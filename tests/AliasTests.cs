using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class AliasTests
{
    private readonly LambdaExpressionEnumerableEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;

    public AliasTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEnumerableEvaluator();
        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapterFactory()
                .Create(dataSet.Map);
    }

    [Fact]
    public void TestSelectColumnAliasStatement()
    {
        const string sql = "SELECT Id as CustomerId, Name as CustomerName FROM dbo.Customers WHERE StateId = 1";
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
            "() => value(tests.Customer[]).Where(c => (c.StateId == 1)).Select(Param_0 => new Dynamic_Customer() {CustomerId = Param_0.Id, CustomerName = Param_0.Name})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"CustomerId\":1,\"CustomerName\":\"Nic\"}]");
    }

    [Fact]
    public void TestSelectTableAliasStatement()
    {
        const string sql = "SELECT c.Id as CustomerId, c.Name as CustomerName FROM dbo.Customers c WHERE c.StateId = 1";
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
            "() => value(tests.Customer[]).Where(c => (c.StateId == 1)).Select(c => new Dynamic_Customer() {CustomerId = c.Id, CustomerName = c.Name})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate(lambda); 

        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"CustomerId\":1,\"CustomerName\":\"Nic\"}]");
    }
}
