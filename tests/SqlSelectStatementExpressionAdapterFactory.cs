namespace tests;
using src;

public class SqlSelectStatementExpressionAdapterFactory {
    
    public SqlSelectStatementExpressionAdapter Create(Dictionary<string, IEnumerable<object>> _map) {
        TypeMapper typeMapper = new TypeMapper(_map);

        ExpressionAdapter expressionAdapter = 
            new ExpressionAdapter(
                typeMapper, 
                new CollectionMapper(_map), 
                new SqlFieldProvider(typeMapper), 
                new FieldMappingProvider(typeMapper, new UniqueNameProviderFactory()),
                new MyObjectBuilder(),
                new EnumerableMethodInfoProvider());

        return 
            new SqlSelectStatementExpressionAdapter(
                expressionAdapter);
    }
}