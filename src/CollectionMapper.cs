namespace src;

public class CollectionMapper {
    private readonly Dictionary<string, IEnumerable<object>> _map;

    public CollectionMapper(Dictionary<string, IEnumerable<object>> map)
    {
        _map = map;
    }

    public IEnumerable<object>? GetMappedCollection(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key];
        }
        return null; 
    }
}
