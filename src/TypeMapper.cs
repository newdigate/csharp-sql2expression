namespace src;

public class TypeMapper : ITypeMapper  {
    private readonly Dictionary<string, IEnumerable<object>> _map;

    public TypeMapper(Dictionary<string, IEnumerable<object>> map)
    {
        _map = map;
    }

    public IEnumerable<string> GetKeys()
    {
        return _map.Keys;
    }

    public Type? GetMappedType(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key].GetType().GetElementType();
        }
        return null;
    }   
}
