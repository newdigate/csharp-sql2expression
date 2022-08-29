using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using Newtonsoft.Json;

namespace tests;
using src;

public class SelectCountStarTests
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;

    public SelectCountStarTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapterFactory()
                .Create(dataSet.Map);
    }

    [Fact]
    public void TestCountStarStatement()
    {
        const string sql = "SELECT count(*) FROM dbo.Customers";
        var parseResult = Parser.Parse(sql);

        SqlSelectStatement? selectStatement =
            parseResult.Script.Batches
                .SelectMany( b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Xunit.Assert.NotNull(lambda);
        string expressionString = lambda.ToString();
        Xunit.Assert.Equal(
            "() => value(tests.Customer[]).Count()",
            expressionString);

        int result = _lambdaEvaluator.Evaluate<int>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        Xunit.Assert.Equal(1,result);
    }
}
