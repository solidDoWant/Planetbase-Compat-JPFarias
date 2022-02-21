using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Planetbase_Compat_JPFarias
{
    /// <summary>
    ///     Provides extension helper methods for building method bodies.
    /// </summary>
    public static class ILProcessorExtensions
    {
        public static void AddNopInstruction(this ILProcessor ilProcessor)
        {
            ilProcessor.Append(ilProcessor.Create(OpCodes.Nop));
        }

        public static void AddReturnInstruction(this ILProcessor ilProcessor)
        {
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
        }

        public static void PushParametersOntoStack(this ILProcessor ilProcessor,
            IEnumerable<ParameterDefinition> targetMethodParameters)
        {
            foreach (var parameter in targetMethodParameters) ilProcessor.PushParameterOntoStack(parameter);
        }

        public static void PushInstanceOntoStack(this ILProcessor ilProcessor)
        {
            // For instance methods, <c>this</c> should always be the first parameter.
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
        }

        public static void PushParameterOntoStack(this ILProcessor ilProcessor, ParameterDefinition parameter)
        {
            ilProcessor.PushParameterOntoStack(parameter.Sequence);
        }

        public static void PushParameterOntoStack(this ILProcessor ilProcessor, int parameterNumber)
        {
            // ldarg is not the most efficient instruction for most cases, but it should cover all cases.
            // An extra two bytes (worst case) per patch shouldn't make a big difference here.
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg, parameterNumber));
        }

        public static void CallMethod(this ILProcessor ilProcessor, MethodReference redirectorMethod)
        {
            ilProcessor.Append(ilProcessor.Create(OpCodes.Call, redirectorMethod));
        }

        public static void StoreCallResult(this ILProcessor ilProcessor, TypeReference returnType)
        {
            ilProcessor.Append(ilProcessor.Create(OpCodes.Stobj, returnType));
        }

        public static void ReturnBoolean(this ILProcessor ilProcessor, TypeReference booleanType, bool value)
        {
            // The instruction set used here is based off what the compiler produces.
            // No idea why so many instructions are needed.
            var returnValue = value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;

            ilProcessor.Body.Variables.Add(new VariableDefinition(booleanType));

            ilProcessor.Append(ilProcessor.Create(returnValue));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Stloc_0));
            var ins = ilProcessor.Create(OpCodes.Ldloc_0);
            ilProcessor.Append(ins);
            ilProcessor.InsertBefore(ins, ilProcessor.Create(OpCodes.Br_S, ins));
            ilProcessor.AddReturnInstruction();
        }

        // ReSharper disable once UnusedMember.Global
        public static void ReturnULong(this ILProcessor ilProcessor, ulong value)
        {
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_I8, (long) value));
            ilProcessor.AddReturnInstruction();
        }
    }
}