using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using PlanetbaseFramework;
using UnityEngine;

namespace Planetbase_Compat_JPFarias.Processors
{
    /// <summary>
    ///     Extracts data from a Redirector attribute described by a Mono.Cecil CustomAttribute.
    /// </summary>
    public class RedirectorAttributeData
    {
        public bool ShouldRedirectFrom { get; protected set; }
        public ulong BitSetOption { get; protected set; }
        public TypeReference TargetedClass { get; protected set; }

        public RedirectorAttributeData(CustomAttribute redirectionAttribute)
        {
            var constructorArguments = redirectionAttribute.ConstructorArguments;

            SetShouldRedirectFromArg(redirectionAttribute);
            ValidateArgumentCount(constructorArguments);
            PopulateFromConstructorArguments(constructorArguments);
        }

        protected void SetShouldRedirectFromArg(ICustomAttribute redirectionAttribute)
        {
            var attributeTypeName = redirectionAttribute.AttributeType.FullName;
            switch (attributeTypeName)
            {
                case "Redirection.RedirectFromAttribute":
                    ShouldRedirectFrom = true;
                    return;
                case "Redirection.RedirectToAttribute":
                    ShouldRedirectFrom = false;
                    return;
                default:
                    Debug.Log(
                        "Unsupported redirection attribute detected! Please raise an issue on Github with solidDoWant.");
                    throw new NotSupportedException($"The attribute is of unsupported type \"{attributeTypeName}\"");
            }
        }

        protected static void ValidateArgumentCount(ICollection<CustomAttributeArgument> constructorArguments)
        {
            var argumentCount = constructorArguments.Count;
            const int expectedArgumentCount = 2;

            if (argumentCount == expectedArgumentCount)
                return;

            var adjective = argumentCount < expectedArgumentCount ? "few" : "many";
            throw new NotSupportedException(
                $"Attribute has too {adjective} constructor arguments. Expected {expectedArgumentCount}, got {argumentCount}."
            );
        }

        protected void PopulateFromConstructorArguments(IEnumerable<CustomAttributeArgument> constructorArguments)
        {
            foreach (var constructorArgumentValue in constructorArguments.Select(arg => arg.Value))
                switch (constructorArgumentValue)
                {
                    case ulong value:
                        BitSetOption = value;
                        break;
                    case TypeReference value:
                        TargetedClass = value;
                        break;
                    default:
                        var message =
                            $"Unexpected parameter of type \"{constructorArgumentValue.GetType()}\" found. Attribute not supported.";
                        Debug.Log(message);
                        throw new NotSupportedException(message);
                }
        }

        public static RedirectorAttributeData TryBuildRedirectorAttributeData(CustomAttribute attribute)
        {
            try
            {
                return new RedirectorAttributeData(attribute);
            }
            catch (Exception e)
            {
                Debug.Log($"Failed to build a patch for attribute {attribute}");
                Utils.LogException(e);
                return null;
            }
        }
    }
}