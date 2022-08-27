using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;

namespace src;

public interface IExpressionAdapter
{
    LambdaExpression? CreateExpression(SqlTableExpression expression, out Type? elementType, Type? joinOutputType);
    LambdaExpression? CreateJoinExpression(SqlJoinTableExpression sqlJoinStatement, Type? joinOutputType, string outerParameterName, string innerParameterName, SqlConditionClause onClause, out Type elementType);
    LambdaExpression? CreateRefExpression(SqlTableRefExpression sqlTableRefExpression, Type elementType, IEnumerable<object> elementArray, string parameterName);
    LambdaExpression CreateSelectExpression(SqlSelectClause selectClause, Type inputType, string parameterName, out Type? outputType);
    LambdaExpression? CreateSourceExpression(SqlFromClause fromClause, out Type? elementType, out string? tableRefExpressionAlias);
    LambdaExpression? CreateSourceExpression(SqlTableExpression expression, out Type? elementType, out string? tableRefExpressionAlias);
    LambdaExpression? CreateWhereExpression(SqlTableRefExpression sqlTableRefExpression, Type mappedType, string parameterName);
    LambdaExpression CreateWhereExpression(SqlWhereClause whereClause, Type elementType);
    LambdaExpression ProcessSelectStatement(SqlQuerySpecification query, out Type? outputType);
}
