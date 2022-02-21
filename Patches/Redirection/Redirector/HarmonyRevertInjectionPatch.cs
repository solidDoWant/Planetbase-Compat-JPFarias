namespace Planetbase_Compat_JPFarias.Patches.Redirection.Redirector
{
    public class HarmonyRevertInjectionPatch
    {
        // ReSharper disable once UnusedMember.Global
        public static bool Prefix()
        {
            HarmonyInjectionPatch.GetCallingMod()?.GetHarmonyInstance().UnpatchAll();

            // Don't run any of the redirector code
            return false;
        }
    }
}