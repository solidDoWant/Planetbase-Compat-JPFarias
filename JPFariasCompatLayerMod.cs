using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Harmony;
using Planetbase_Compat_JPFarias.DynamicTypeBases;
using Planetbase_Compat_JPFarias.Patches.Redirection.Redirector;
using Planetbase_Compat_JPFarias.Processors;
using PlanetbaseFramework;
using UnityEngine;

namespace Planetbase_Compat_JPFarias
{
    // ReSharper disable once UnusedMember.Global
    /// <summary>
    ///     Provides a "compatibility layer" for JPFarias mods. This dynamically converts and loads
    ///     JPFarias mods into PB framework mod, and fixes compability issues with PB framework
    ///     libraries.
    /// </summary>
    public class JPFariasCompatLayerMod : ModBase
    {
        public const string AssemblyVersion = "1.0.0.0";
        public new static readonly Version ModVersion = new Version(AssemblyVersion);

        public static List<JPFariasMod> ModList = new List<JPFariasMod>();
        public override string ModName => "Planetbase Framework compatibility layer for JPFarias mods";
        public bool IsRedirectorLoadedAndPatched { get; protected set; }

        public override void Init()
        {
            // Get DLLs in the mod folder, excluding this one
            var dllPaths = Directory.GetFiles(BasePath, "*.dll")
                .Select(Path.GetFullPath)
                .Where(dllName => dllName != Assembly.GetExecutingAssembly().Location)
                .ToList();

            Debug.Log($"Found {dllPaths.Count} candidates for JPFarias mods");
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;

            InstantiateMods(dllPaths);
            Debug.Log("JPFarias mod load complete, beginning mod initialization...");
            InitializeMods();
        }

        protected void InstantiateMods(IEnumerable<string> dllPaths)
        {
            var dllProcessor = new DllProcessor();
            var pathCount = 0;
            foreach (var dllPath in dllPaths)
            {
                pathCount++;
                try
                {
                    ModList.AddRange(dllProcessor.ProcessDll(dllPath));
                }
                catch (Exception e)
                {
                    Debug.Log($"Failed to load and initialize mods in {dllPath}.");
                    Utils.LogException(e);
                }
            }

            Debug.Log($"Successfully instantiated {ModList.Count} mods from {pathCount} files");
        }

        /// <summary>
        ///     Initializes the mods in ModList.
        /// </summary>
        protected void InitializeMods()
        {
            var instantiatedModCount = ModList.Count;
            var initializedMods = ModList.Where(TryInitializeMod).ToList();
            ModList = initializedMods;
            Debug.Log($"Successfully initialized {ModList.Count} of {instantiatedModCount} mods");
        }

        /// <summary>
        ///     Initializes a mod. If fails, does so gracefully.
        /// </summary>
        /// <param name="mod">The mod to initialize</param>
        /// <returns>True of the mod successfully initialized, false otherwise.</returns>
        protected bool TryInitializeMod(JPFariasMod mod)
        {
            Debug.Log($"Attempting to initialize {mod.ModName}...");
            try
            {
                mod.Init();
            }
            catch (Exception e)
            {
                HandleFailedMod(mod, e);
                return false;
            }

            HandleInitializedMod(mod);
            return true;
        }

        protected void HandleFailedMod(JPFariasMod mod, Exception e)
        {
            mod.CurrentLoadState = ModState.Failed;
            Debug.Log($"Failed to initialize {mod.ModName}");
            Utils.LogException(e);
        }

        protected void HandleInitializedMod(JPFariasMod mod)
        {
            ModLoader.ModList.Add(mod);
            mod.CurrentLoadState = ModState.Initialized;
            Debug.Log($"{mod.ModName} initialized");
        }

        protected void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly.GetName().Name != "Redirector")
                return;
            if (IsRedirectorLoadedAndPatched)
                return;

            PatchRedirector(args.LoadedAssembly);
            IsRedirectorLoadedAndPatched = true;
        }

        protected void PatchRedirector(Assembly redirectorAssembly)
        {
            Debug.Log("Patching redirector");
            var redirectorType = redirectorAssembly.GetType("Redirection.Redirector");
            GetHarmonyInstance().Patch(redirectorType.GetMethod("PerformRedirections"),
                new HarmonyMethod(typeof(HarmonyInjectionPatch).GetMethod("Prefix")));
            GetHarmonyInstance().Patch(redirectorType.GetMethod("RevertRedirections"),
                new HarmonyMethod(typeof(HarmonyRevertInjectionPatch).GetMethod("Prefix")));
        }
    }
}