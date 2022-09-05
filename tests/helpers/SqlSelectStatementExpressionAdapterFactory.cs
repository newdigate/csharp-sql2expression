namespace tests;
using src;

public class SqlSelectStatementExpressionAdapterFactory {
    public LambdaStringToCSharpConverter CreateLambdaExpressionConverter(Dictionary<string, IEnumerable<object>> map, Dictionary<Type,string> instanceMap) {
        TypeMapper typeMapper = new TypeMapper(map);
        IInstanceMapper instanceMapper = new InstanceMapper( instanceMap );
        return new LambdaStringToCSharpConverter(typeMapper, instanceMapper);
    }
    public SqlSelectStatementExpressionAdapter Create(Dictionary<string, IEnumerable<object>> _map) {
        TypeMapper typeMapper = new TypeMapper(_map);
        IUniqueNameProviderFactory uniqueNameProviderFactory = new UniqueNameProviderFactory();
        ExpressionAdapter expressionAdapter = 
            new ExpressionAdapter(
                typeMapper, 
                new CollectionMapper(_map), 
                new SqlFieldProvider(typeMapper), 
                new FieldMappingProvider(typeMapper, uniqueNameProviderFactory),
                new MyObjectBuilder(),
                new EnumerableMethodInfoProvider(),
                new LambdaExpressionEvaluator(),
                uniqueNameProviderFactory);

        return 
            new SqlSelectStatementExpressionAdapter(
                expressionAdapter);
    }
}