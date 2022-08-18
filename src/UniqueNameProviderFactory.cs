namespace src;

public class UniqueNameProviderFactory : IUniqueNameProviderFactory {
    public IUniqueNameProvider Create() {
        return new UniqueNameProvider();
    }
}
