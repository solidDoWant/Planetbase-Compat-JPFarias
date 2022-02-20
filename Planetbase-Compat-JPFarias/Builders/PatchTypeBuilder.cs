using System.Linq;
using System.Reflection;
using Harmony;
using Mono.Cecil;
using Planetbase_Compat_JPFarias.DynamicTypeBases;
using PlanetbaseFramework.Cecil;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Planetbase_Compat_JPFarias.Builders
{
    public class PatchTypeBuilder
    {
        public const string PatchNamespace = "PlanetbaseFrameworkCompatJPFarias.Patches";

        /// <summary>
        ///     The unique patch ID. This ensures that patch type names are unique.
        /// </summary>
        public static int PatchId { get; protected set; }

        public static ModuleDefinition HarmonyModule { get; } =
            ModuleLoader.LoadByPath(Assembly.GetAssembly(typeof(HarmonyInstance)).Location);

        public static TypeDefinition HarmonyPatchType { get; } = HarmonyModule.GetType("Harmony.HarmonyPatch");
        public TypeReference ImportedBaseType { get; }
        public ModuleDefinition ModDll { get; }
        public MethodDefinition AnnotatedMethod { get; }
        public TypeReference TargetedClass { get; }
        public TypeReference ModuleSpecificTypTypeReference { get; }
        protected TypeDefinition PatchType { get; set; }

        public PatchTypeBuilder(ModuleDefinition modDll, MethodDefinition annotatedMethod,
            TypeReference targetedClass)
        {
            ModDll = modDll;
            AnnotatedMethod = annotatedMethod;
            TargetedClass = targetedClass;

            //Workaround for CS0236
            ModuleSpecificTypTypeReference = new TypeReference("System", "Type", ModDll, ModDll.TypeSystem.Corlib);
            ImportedBaseType = ModDll.Import(typeof(HarmonyPatchParent));
        }

        /// <summary>
        ///     Builds a new type for Harmony to load and use as a patch.
        ///     The type is defined as follows:
        ///     <code>
        ///     namespace PatchNamespace.AttributeData.TargetedClass.FullName
        ///     {
        ///         public class AnnotatedMethod.Name_PatchID : HarmonyPatchParent
        ///         {
        ///             protected AnnotatedMethod.Name_PatchID() : base() {}
        /// 
        ///             public static bool Prefix(TargetedClass __instance, &gt;parameters&lt;,
        ///                 AnnotatedMethod.ReturnType __result)
        ///             {
        ///                 __result = __instance.AnnotatedMethod(&gt;parameters&lt;);
        /// 
        ///                 return false;
        ///             }
        ///         }
        ///     }
        ///     </code>
        /// </summary>
        /// <returns>The created type.</returns>
        public TypeDefinition Build()
        {
            PatchType = DefineNewPatchType(TargetedClass.FullName, AnnotatedMethod.Name, PatchId++);
            PatchType.Methods.Add(BuildConstructor());
            if (AnnotatedMethod.HasParameters)
                PatchType.CustomAttributes.Add(BuildTargetedMethodParameterTypePatchAnnotation());
            PatchType.CustomAttributes.Add(BuildTargetedMethodPatchAnnotation());
            PatchType.CustomAttributes.Add(BuildTargetedTypePatchAnnotation());
            PatchType.Methods.Add(new PrefixMethodBuilder(ModDll, AnnotatedMethod, TargetedClass)
                .Build());
            return PatchType;
        }

        protected TypeDefinition DefineNewPatchType(string targetedTypeFullName, string targetedMethodName,
            int patchNumber)
        {
            const TypeAttributes patchTypeAttributes = TypeAttributes.Class | TypeAttributes.Public |
                                                       TypeAttributes.AnsiClass | TypeAttributes.AutoLayout |
                                                       TypeAttributes.BeforeFieldInit;

            return new TypeDefinition($"{PatchNamespace}.{targetedTypeFullName}",
                $"{targetedMethodName}_{patchNumber}", patchTypeAttributes, ImportedBaseType);
        }

        protected MethodDefinition BuildConstructor()
        {
            return new ConstructorMethodBuilder(ModDll, ImportedBaseType).Build();
        }

        protected CustomAttribute BuildTargetedTypePatchAnnotation()
        {
            return BuildPatchAttribute(ModuleSpecificTypTypeReference, TargetedClass);
        }

        protected CustomAttribute BuildTargetedMethodPatchAnnotation()
        {
            return BuildPatchAttribute(HarmonyModule.TypeSystem.String, AnnotatedMethod.Name);
        }

        protected CustomAttribute BuildTargetedMethodParameterTypePatchAnnotation()
        {
            var arrayTypeType = new ArrayType(ModuleSpecificTypTypeReference);
            var methodParameterTypesArgument = AnnotatedMethod.Parameters.Select(parameter =>
                new CustomAttributeArgument(ModuleSpecificTypTypeReference, parameter.ParameterType)).ToArray();
            return BuildPatchAttribute(arrayTypeType, methodParameterTypesArgument);
        }

        protected CustomAttribute BuildPatchAttribute(TypeReference argumentType, object argumentValue)
        {
            var patchAttribute = new CustomAttribute(GetHarmonyPatchConstructor(argumentType));
            patchAttribute.ConstructorArguments.Add(new CustomAttributeArgument(argumentType, argumentValue));
            return patchAttribute;
        }

        protected MethodReference GetHarmonyPatchConstructor(TypeReference argumentType)
        {
            return ModDll.Import(
                HarmonyPatchType
                    .Methods
                    .First(m =>
                        m.IsConstructor &&
                        m.Parameters.Count == 1 &&
                        m.Parameters[0].ParameterType.IsSameTypeAs(argumentType)
                    )
            );
        }
    }
}