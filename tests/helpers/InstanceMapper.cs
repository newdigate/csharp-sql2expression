namespace tests;
using src;

public class InstanceMapper : IInstanceMapper
{
    private readonly Dictionary<Type, string> _map;

    public InstanceMapper(Dictionary<Type, string> map)
    {
        _map = map;
    }

    public string? GetInstanceName(Type type)
    {
        if (_map.ContainsKey(type))
            return _map[type];
        return null;
    }
}
