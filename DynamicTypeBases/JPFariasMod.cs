using System.Collections.Generic;
using PlanetbaseFramework;

namespace Planetbase_Compat_JPFarias.DynamicTypeBases
{
    /// <summary>
    ///     Base type for converted JPFarias mods. The ModTypeProcessor changes the base type
    ///     of a JPFarias mod to this class, allowing it to be used by the PB framework.
    /// </summary>
    public abstract class JPFariasMod : ModBase
    {
        public static List<JPFariasMod> JPFariasMods { get; } = new List<JPFariasMod>();

        public override string ModName => $"JPFarias' {GetType().Name}";
        public ModState CurrentLoadState { get; set; }

        protected JPFariasMod()
        {
            CurrentLoadState = ModState.Pending;
            JPFariasMods.Add(this);
        }
    }
}