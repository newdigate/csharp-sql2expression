namespace src;

public interface IMyObjectBuilder
{
    Type CompileResultType(string typeSignature, IEnumerable<Field> Fields);
    Type CompileResultType(string typeSignature, IEnumerable<Field> leftFields, IEnumerable<Field> rightFields, string leftPropertyName, string rightPropertyName);
}
