namespace src;

/*
public class CreateLambdaExpressionForSqlTableExpression {
    
    public LambdaExpression CreateExpression(SqlTableExpression expression, Type elementType, IEnumerable<object> elements, string parameterName) {
        switch (expression) {
            case SqlJoinTableExpression sqlJoinStatement: 
                return null;// CreateJoinExpression(sqlJoinStatement, );
                break;
            case SqlTableRefExpression sqlTableRefStatement: 
                return CreateRefExpression(sqlTableRefStatement, elementType, elements, parameterName );
                break;
        }
        return null;
    }

    
}
*/
public class TypeMapper {
    private readonly Dictionary<string, IEnumerable<object>> _map;

    public TypeMapper(Dictionary<string, IEnumerable<object>> map)
    {
        _map = map;
    }
    public Type? GetMappedType(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key].GetType().GetElementType();
        }
        return null;
    }   
}
