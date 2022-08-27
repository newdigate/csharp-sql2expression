namespace src;

public interface IMyObjectBuilder
{
    Type CompileResultType(string typeSignature, IEnumerable<Field> Fields);
}
