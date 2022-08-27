namespace src;

public interface ICollectionMapper {
     public IEnumerable<object>? GetMappedCollection(string key);
}
