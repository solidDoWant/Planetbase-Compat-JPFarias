using Mono.Cecil;

namespace Planetbase_Compat_JPFarias.Builders
{
    public class ConstructorMethodBuilder
    {
        public ModuleDefinition ModDll { get; }
        public TypeReference BaseType { get; }

        public ConstructorMethodBuilder(ModuleDefinition modDll, TypeReference baseType)
        {
            ModDll = modDll;
            BaseType = baseType;
        }

        public MethodDefinition Build()
        {
            var baseTypeConstructorReference =
                new MethodReference(".ctor", ModDll.TypeSystem.Void, BaseType) {HasThis = true};

            const MethodAttributes constructMethodAttributes = MethodAttributes.Family |
                                                               MethodAttributes.HideBySig |
                                                               MethodAttributes.SpecialName |
                                                               MethodAttributes.RTSpecialName;

            var constructor = new MethodDefinition(".ctor", constructMethodAttributes, ModDll.TypeSystem.Void);

            var ilProcessor = constructor.Body.GetILProcessor();
            ilProcessor.PushInstanceOntoStack();
            ilProcessor.CallMethod(baseTypeConstructorReference);
            ilProcessor.AddNopInstruction();
            ilProcessor.AddReturnInstruction();

            return constructor;
        }
    }
}