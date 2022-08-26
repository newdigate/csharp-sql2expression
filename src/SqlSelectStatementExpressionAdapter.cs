using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;

namespace src;

public class SqlSelectStatementExpressionAdapter {
    private readonly ExpressionAdapter _expressionAdapter;

    public SqlSelectStatementExpressionAdapter(ExpressionAdapter expressionAdapter)
    {
        _expressionAdapter = expressionAdapter;
    }

    public LambdaExpression? ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        switch (selectStatement.SelectSpecification.QueryExpression) {
            case SqlQuerySpecification sqlQuerySpecification: return  _expressionAdapter.ProcessSelectStatement(sqlQuerySpecification, out Type? outputType);
        }
        return null;
    }
}