using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Planetbase_Compat_JPFarias.Builders;
using Planetbase_Compat_JPFarias.Patches.Redirection.Redirector;
using PlanetbaseFramework;
using PlanetbaseFramework.Cecil;
using UnityEngine;

namespace Planetbase_Compat_JPFarias.Processors
{
    public class RedirectorTypeProcessor
    {
        public ModuleDefinition ModDll { get; }

        public RedirectorTypeProcessor(ModuleDefinition modDll)
        {
            ModDll = modDll;
        }

        public IEnumerable<TypeDefinition> ProcessRedirectorType(TypeDefinition typeDefinition)
        {
            return typeDefinition.Methods
                .Where(method => method.HasCustomAttributes && !method.IsAbstract && method.IsPublic &&
                                 !method.HasGenericParameters)
                .SelectMany(
                    annotatedMethod => annotatedMethod.CustomAttributes
                        .Where(attribute =>
                            attribute.HasConstructorArguments &&
                            attribute.AttributeType.HasTypeAsParent("Redirection.RedirectAttribute"))
                        .Select(RedirectorAttributeData.TryBuildRedirectorAttributeData)
                        .Where(attributeData => attributeData != null),
                    BuildTypeForAttribute
                );
        }

        public TypeDefinition BuildTypeForAttribute(MethodDefinition annotatedMethod,
            RedirectorAttributeData attributeData)
        {
            if (!attributeData.ShouldRedirectFrom)
            {
                Debug.Log(
                    "ShouldRedirectFrom attributes not currently supported. Please raise an issue on GitHub."
                );
                return null;
            }

            TypeDefinition patchType;
            try
            {
                patchType = new PatchTypeBuilder(ModDll, annotatedMethod, attributeData.TargetedClass).Build();
            }
            catch (Exception e)
            {
                Debug.Log(
                    $"Failed to build a patch for {annotatedMethod.FullName} in class {attributeData.TargetedClass.FullName} in {ModDll.Name}");
                Utils.LogException(e);
                return null;
            }

            HarmonyInjectionPatch.RegisterNewType(patchType, attributeData.BitSetOption);
            return patchType;
        }
    }
}