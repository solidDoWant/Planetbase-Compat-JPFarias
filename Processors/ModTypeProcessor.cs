using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Planetbase_Compat_JPFarias.DynamicTypeBases;
using PlanetbaseFramework.Cecil;
using UnityEngine;

namespace Planetbase_Compat_JPFarias.Processors
{
    public class ModTypeProcessor
    {
        public TypeReference CompatModReference { get; }
        public MethodReference ParentModConstructorReference { get; }
        public ModuleDefinition TargetedDll { get; }
        public LinkedList<TypeReference> PatchedTypes { get; } = new LinkedList<TypeReference>();

        public ModTypeProcessor(ModuleDefinition targetedDll)
        {
            TargetedDll = targetedDll;

            //Workaround for CS0236
            CompatModReference = TargetedDll.Import(
                ModuleLoader
                    .LoadByPath(Assembly.GetExecutingAssembly().Location)
                    .GetType(typeof(JPFariasMod).FullName)
            );

            ParentModConstructorReference = TargetedDll.Import(
                CompatModReference
                    .Resolve()
                    .Methods
                    .First(
                        method =>
                            method.IsConstructor &&
                            !method.HasParameters
                    )
            );
        }

        /// <summary>
        ///     Checks if the provided type is a JPFarias IMod. If so, the type is converted to a
        ///     PB framework mod. Parent types may be conditionally affected.
        /// </summary>
        /// <param name="type">The name of the type to check</param>
        /// <returns>True if the type is a (now patched) mod, false otherwise.</returns>
        public bool ProcessModType(TypeDefinition type)
        {
            if (!IsTypeInstantiatable(type))
                return false;

            // Check the interfaces on the type to see if they include IMod
            var iModInterface = GetIModInterface(type);

            if (iModInterface == null)
                // Type is not a JPFarias mod
                return false;

            Debug.Log($"Found JPFarias mod of type \"{type.FullName}\"");
            UpdateType(type, iModInterface);
            return true;
        }

        protected void UpdateType(TypeDefinition type, TypeReference iModInterface)
        {
            // Update the type to change from IMod to ModBase
            type.Interfaces.Remove(iModInterface);
            UpdateTypeMethods(type);
            ChangeParentType(type);
        }

        protected void ChangeParentType(TypeDefinition childType)
        {
            // Traverse the type tree to find the parent type that inherits from System.Object
            var typeToPatch = childType;
            while (!childType.BaseType.IsSameTypeAs(TargetedDll.TypeSystem.Object))
                typeToPatch = childType.BaseType.Resolve();

            // Prevent double patching
            if (PatchedTypes.Contains(typeToPatch))
                return;

            typeToPatch.BaseType = CompatModReference;
            typeToPatch.BaseType.Resolve();

            // Add a call to base constructor
            var childConstructor = typeToPatch.Methods.First(method => method.IsConstructor && !method.HasParameters);

            // ctor method body should always start with:
            // ldarg.0 (loads 'this' onto stack)
            // call instance <parent ctor>
            // Therefore by replacing the second argument we can change the constructor it calls
            var ilProcessor = childConstructor.Body.GetILProcessor();
            ilProcessor.Replace(childConstructor.Body.Instructions[1],
                ilProcessor.Create(OpCodes.Call, ParentModConstructorReference));

            PatchedTypes.AddLast(typeToPatch);
        }

        protected void UpdateTypeMethods(TypeDefinition type)
        {
            // Find the Init and Update methods and make them <c>override</c> the corresponding methods in ModBase
            var patchCount = 0;
            foreach (var method in type.Methods.Where(method => method.Name == "Init" || method.Name == "Update"))
            {
                method.IsVirtual = true;
                method.IsHideBySig = true;
                method.IsFinal = false;
                method.IsNewSlot = false;
                patchCount++;

                // This prevents continued enumeration after both Init and Update have been found/patched
                if (patchCount >= 2)
                    return;
            }
        }

        protected static TypeReference GetIModInterface(TypeDefinition type)
        {
            return type.Interfaces.FirstOrDefault(@interface => @interface.FullName == "Planetbase.IMod");
        }

        public static bool IsTypeInstantiatable(TypeDefinition type)
        {
            return type.IsClass && !type.IsAbstract && type.IsPublic;
        }
    }
}