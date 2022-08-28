using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;

namespace src;

public interface IExpressionAdapter
{
    LambdaExpression ConvertSqlSelectQueryToLambda(SqlQuerySpecification query, out Type? outputType, bool isSqlInConditionBooleanQueryExpression = false);
}
