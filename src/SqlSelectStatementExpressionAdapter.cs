using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;

namespace src;

public class SqlSelectStatementExpressionAdapter : ISqlSelectStatementExpressionAdapter
{
    private readonly IExpressionAdapter _expressionAdapter;

    public SqlSelectStatementExpressionAdapter(IExpressionAdapter expressionAdapter)
    {
        _expressionAdapter = expressionAdapter;
    }

    public LambdaExpression? ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        switch (selectStatement.SelectSpecification.QueryExpression)
        {
            case SqlQuerySpecification sqlQuerySpecification:
                return _expressionAdapter.ConvertSqlSelectQueryToLambda(sqlQuerySpecification, out Type? outputType);
        }
        return null;
    }
}