using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Planetbase_Compat_JPFarias.DynamicTypeBases;
using PlanetbaseFramework;
using PlanetbaseFramework.Cecil;
using UnityEngine;

namespace Planetbase_Compat_JPFarias.Processors
{
    public class DllProcessor
    {
        protected List<string> ProcessedDLLs { get; } = new List<string>();

        /// <summary>
        ///     Searches a DLL for JPFarias mods, then converts them and creates Redirector patches where applicable.
        ///     Instantiates created mods.
        /// </summary>
        /// <param name="dll">The DLL to search</param>
        /// <returns>The converted, instantiated mods.</returns>
        // Note: This may not work in the case of three assemblies: A, B, and C.
        // Assembly A contains a JPFarias IMod, which calls a method in
        // assembly B. The method in in assembly B calls
        // Redirector.PerformRedirections in assembly C. Fixing this is not
        // trivial as it would require somehow patching assembly B before the
        // CLR automatically loads the assembly.
        // This could possibly be done by searching through assembly A's
        // assembly references for references to assembly C, patching assembly
        // B if some are found, then preemptively loading assembly B into the
        // CLR before A is loaded, preventing the CLR's assembly resolver from
        // attempting to load assembly B from disk. However the effort required
        // to implement a fix for this corner case is not worth it at this
        // time. Please raise an issue on GitHub if you have a use case for
        // this functionality.
        public IEnumerable<JPFariasMod> ProcessDll(string dllPath)
        {
            var dllModule = ModuleLoader.LoadByPath(dllPath);

            Debug.Log($"Checking for and attempting to load JPFarias mods from \"{dllModule.Name}\"...");
            if (ProcessedDLLs.Contains(dllModule.Name))
                Debug.Log("DLL already processed, skipping");
            ProcessedDLLs.Add(dllModule.Name);

            var modTypeReferences = new LinkedList<TypeDefinition>(ProcessIModTypes(dllModule));
            if (!modTypeReferences.Any())
            {
                Debug.Log($"Found no JPFarias mods in \"{dllModule.Name}\"");
                return Enumerable.Empty<JPFariasMod>();
            }

            CreateRedirectorPatches(dllModule);
            return LoadModsFromModifiedDLL(dllModule, modTypeReferences);
        }

        /// <summary>
        ///     Converts JPFarias mods to PB Framework mods.
        /// </summary>
        /// <param name="targetedDll">The DLL to search</param>
        /// <returns>The converted mod types.</returns>
        protected IEnumerable<TypeDefinition> ProcessIModTypes(ModuleDefinition targetedDll)
        {
            var typeProcessor = new ModTypeProcessor(targetedDll);
            return targetedDll.Types.Where(type => typeProcessor.ProcessModType(type));
        }

        /// <summary>
        ///     Converts JPFarias' Redirector patches to Harmony patches.
        /// </summary>
        /// <param name="targetedDll">The DLL to search</param>
        protected void CreateRedirectorPatches(ModuleDefinition targetedDll)
        {
            var doesDllReferenceRedirector =
                targetedDll.AssemblyReferences.Any(reference => reference.Name == "Redirector");
            if (!doesDllReferenceRedirector) return;

            Debug.Log("Building patches for Redirector methods");
            var typeProcessor = new RedirectorTypeProcessor(targetedDll);

            // Using a temporary variable prevents modifying the collection while iterating over it
            var newPatches = new LinkedList<TypeDefinition>();
            foreach (var newPatch in targetedDll.Types.SelectMany(targetedDllType =>
                         typeProcessor.ProcessRedirectorType(targetedDllType)))
                newPatches.AddLast(newPatch);
            foreach (var newPatch in newPatches)
                targetedDll.Types.Add(newPatch);
        }

        /// <summary>
        ///     Loads the modified DLL and instantiates contained mods of the specified types.
        /// </summary>
        /// <param name="modifiedDll">The patched DLL to load</param>
        /// <param name="modTypeReferences">The mod types to instantiate</param>
        /// <returns>The instantiated mods.</returns>
        protected static IEnumerable<JPFariasMod> LoadModsFromModifiedDLL(ModuleDefinition modifiedDll,
            IEnumerable<TypeDefinition> modTypeReferences)
        {
            var loadedModAssembly = LoadModuleDefinition(modifiedDll);

            var loadedModCount = 0;
            foreach (var mod in modTypeReferences
                         .Where(ModLoader.IsTypeDefinitionValidMod)
                         .Select(type => LoadMod(loadedModAssembly, type))
                         .Where(mod => mod != null)
                    )
            {
                mod.CurrentLoadState = ModState.Instantiated;
                loadedModCount++;
                yield return mod;
            }

            Debug.Log($"Loaded {loadedModCount} JPFarias mods from \"{modifiedDll.Name}\"");
        }

        /// <summary>
        ///     Instantiates a mod of a given type from the given assembly.
        /// </summary>
        /// <param name="modAssembly">The assembly containing the mod type</param>
        /// <param name="modType">The type to load</param>
        /// <returns>The instantiated mod.</returns>
        protected static JPFariasMod LoadMod(Assembly modAssembly, TypeReference modType)
        {
            return ModLoader.LoadMod<JPFariasMod>(modAssembly, modType.FullName);
        }

        /// <summary>
        ///     Loads the given module into the common language runtime as an assembly.
        /// </summary>
        /// <param name="moduleDefinition">The module definition to load</param>
        /// <returns>The assembly loaded into the CLR.</returns>
        protected static Assembly LoadModuleDefinition(ModuleDefinition moduleDefinition)
        {
            Debug.Log($"Attempting to rebuild and load modified \"{moduleDefinition.Name}\" assembly...");

            // Gets the assembly's bytes in COFF
            byte[] assemblyBytes;
            using (var ms = new MemoryStream())
            {
                moduleDefinition.Write(ms);
                assemblyBytes = ms.ToArray();
            }

            var loadedAssembly = Assembly.Load(assemblyBytes);
            Debug.Log($"Successfully loaded assembly \"{moduleDefinition.Name}\"");
            return loadedAssembly;
        }
    }
}