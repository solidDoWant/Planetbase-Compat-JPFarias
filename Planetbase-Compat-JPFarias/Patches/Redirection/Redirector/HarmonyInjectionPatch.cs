using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Harmony;
using Mono.Cecil;
using Planetbase_Compat_JPFarias.DynamicTypeBases;
using PlanetbaseFramework;
using UnityEngine;

namespace Planetbase_Compat_JPFarias.Patches.Redirection.Redirector
{
    public class HarmonyInjectionPatch
    {
        public const string PatchedMethodName = "Redirector.PerformRedirections";

        protected static Dictionary<string, ulong> PatchBitSetRequiredOptions { get; } =
            new Dictionary<string, ulong>();

        protected static LinkedList<Assembly> PatchedAssemblies { get; } = new LinkedList<Assembly>();

        // ReSharper disable once UnusedMember.Global
        public static bool Prefix(ulong bitMask)
        {
            var mod = GetCallingMod();

            if (mod == null)
            {
                Debug.Log($"Failed to find the mod calling \"{PatchedMethodName}\".");
                return false;
            }

            if (mod.CurrentLoadState == ModState.Pending || mod.CurrentLoadState == ModState.Failed)
            {
                Debug.Log($"Mod \"{mod.ModName}\" was in an invalid state while calling {PatchedMethodName}.");
                return false;
            }

            PatchModTypes(mod, bitMask);

            // Don't run any of the redirector code
            return false;
        }

        protected static void PatchModTypes(JPFariasMod mod, ulong bitMask)
        {
            var modAssembly = mod.GetType().Assembly;
            if (PatchedAssemblies.Contains(modAssembly))
                return;
            PatchedAssemblies.AddLast(modAssembly);

            var harmonyInstance = mod.GetHarmonyInstance();
            foreach (var type in modAssembly.GetTypes())
            {
                if (!DoesTypePassBitSetCheck(type, bitMask))
                    continue;

                var harmonyMethods = type.GetHarmonyMethods();
                if (harmonyMethods == null || harmonyMethods.Count <= 0)
                    continue;

                var attributes = HarmonyMethod.Merge(harmonyMethods);
                new PatchProcessor(harmonyInstance, type, attributes).Patch();
            }
        }

        /// <summary>
        ///     Emulates the "BitSetRequiredOption" logic in Redirector.
        /// </summary>
        /// <param name="type">The patch type to check</param>
        /// <param name="bitMask">The bitmask value provided to the PerformRedirections call</param>
        /// <returns>True if the patch should be loaded, false otherwise.</returns>
        protected static bool DoesTypePassBitSetCheck(Type type, ulong bitMask)
        {
            var bitSetRequiredOption = GetBitSetRequiredOptionForType(type);
            if (!bitSetRequiredOption.HasValue)
                return true;

            var bitSetValue = bitSetRequiredOption.Value;
            return bitSetValue == 0 || (bitSetValue & bitMask) != 0;
        }

        public static JPFariasMod GetCallingMod()
        {
            var modType = Utils.GetCallingModType<JPFariasMod>(1);
            return modType == null ? null : JPFariasMod.JPFariasMods.FirstOrDefault(mod => mod.GetType() == modType);
        }

        public static void RegisterNewType(TypeReference newType, ulong bitSetRequiredOption)
        {
            PatchBitSetRequiredOptions.Add(newType.FullName, bitSetRequiredOption);
        }

        public static ulong? GetBitSetRequiredOptionForType(Type type)
        {
            if (type.FullName == null)
                return null;

            if (PatchBitSetRequiredOptions.TryGetValue(type.FullName, out var bitSetRequiredOption))
                return bitSetRequiredOption;

            return null;
        }
    }
}