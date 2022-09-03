using System.Reflection;
using System.Reflection.Emit;

namespace src;

//  https://stackoverflow.com/questions/15641339/create-new-propertyinfo-object-on-the-fly
public class MyObjectBuilder : IMyObjectBuilder
{
    public Type CompileResultType(string typeSignature, IEnumerable<Field> Fields)
    {
        TypeBuilder tb = GetTypeBuilder(typeSignature);
        ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

        // NOTE: assuming your list contains Field objects with fields FieldName(string) and FieldType(Type)
        foreach (var field in Fields)
            CreateProperty(tb, field.FieldName, field.FieldType);

        Type objectType = tb.CreateType();
        return objectType;
    }

    private TypeBuilder GetTypeBuilder(string typeSignature)
    {
        var an = new System.Reflection.AssemblyName(typeSignature);
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder tb =
            moduleBuilder
                .DefineType(
                    typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                    null);

        ConstructorInfo classCtorInfo = typeof(DynamicDataSetElementAttribute).GetConstructor(new Type[] { });
        CustomAttributeBuilder myCABuilder2 = new CustomAttributeBuilder(
                        classCtorInfo,
                        new object[] { });
        tb.SetCustomAttribute(myCABuilder2);
        return tb;
    }

    private void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
    {
        FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

        PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);

        Type[] ctorParams = new Type[] { };
        ConstructorInfo classCtorInfo = typeof(System.Runtime.Serialization.DataMemberAttribute).GetConstructor(ctorParams);

        PropertyInfo? dataMemberNameProperty = typeof(System.Runtime.Serialization.DataMemberAttribute).GetProperty("Name");
        CustomAttributeBuilder myCABuilder2 = new CustomAttributeBuilder(
                        con: classCtorInfo,
                        constructorArgs: new object[] { },
                        namedProperties: new[] { dataMemberNameProperty },
                        propertyValues: new object[] { propertyName.Replace("_", ".") });

        propertyBuilder.SetCustomAttribute(myCABuilder2);

        MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
        ILGenerator getIl = getPropMthdBldr.GetILGenerator();

        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getIl.Emit(OpCodes.Ret);

        MethodBuilder setPropMthdBldr =
            tb.DefineMethod("set_" + propertyName,
              MethodAttributes.Public |
              MethodAttributes.SpecialName |
              MethodAttributes.HideBySig,
              null, new[] { propertyType });

        ILGenerator setIl = setPropMthdBldr.GetILGenerator();
        Label modifyProperty = setIl.DefineLabel();
        Label exitSet = setIl.DefineLabel();

        setIl.MarkLabel(modifyProperty);
        setIl.Emit(OpCodes.Ldarg_0);
        setIl.Emit(OpCodes.Ldarg_1);
        setIl.Emit(OpCodes.Stfld, fieldBuilder);

        setIl.Emit(OpCodes.Nop);
        setIl.MarkLabel(exitSet);
        setIl.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getPropMthdBldr);
        propertyBuilder.SetSetMethod(setPropMthdBldr);
    }

    public Type CompileResultType(string typeSignature, IEnumerable<Field> leftFields, IEnumerable<Field> rightFields, string leftPropertyName, string rightPropertyName)
    {
        Type rightHandSideType = CompileResultType($"{typeSignature}_RHS", rightFields);
        
        TypeBuilder tb = GetTypeBuilder(typeSignature);
        ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        
        foreach (var field in leftFields)
            CreateProperty(tb, field.FieldName, field.FieldType);

        if (rightFields.Count() == 1) { 
            CreateProperty(tb, 
                rightPropertyName.Replace(".", "_"), 
                typeof(IEnumerable<>)
                    .MakeGenericType(rightFields.First().FieldType )
            );
        } else {
            CreateProperty(tb, 
                rightPropertyName.Replace(".", "_"), 
                typeof(IEnumerable<>)
                    .MakeGenericType(rightHandSideType));
        }

        Type objectType = tb.CreateType();
        return objectType;
    }
}

public class DynamicDataSetElementAttribute : Attribute {

}