using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;

namespace src;

public interface ISqlSelectStatementExpressionAdapter
{
    LambdaExpression? ProcessSelectStatement(SqlSelectStatement selectStatement);
}
