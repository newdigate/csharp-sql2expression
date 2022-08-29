namespace src;

public interface ITypeMapper {
    Type? GetMappedType(string key);
    IEnumerable<string> GetKeys();
}

public interface IInstanceMapper {
     string? GetInstanceName(Type type);

}
