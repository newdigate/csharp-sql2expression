using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class EmbeddedQueryTests
{
    private readonly LambdaExpressionEnumerableEvaluator _lambdaEvaluator;

    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;

    public EmbeddedQueryTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEnumerableEvaluator();
        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapterFactory()
                .Create(dataSet.Map);
    }

    [Fact]
    public void TestEmbeddedSelectStatement()
    {
        const string sql = "SELECT c.Name, c.Id FROM (SELECT Id, Name FROM dbo.Customers WHERE StateId = 1) c";
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
            "() => value(tests.Customer[]).Where(c => (c.StateId == 1)).Select(Param_0 => new Dynamic_Customer() {Id = Param_0.Id, Name = Param_0.Name}).Select(c => new Dynamic_Dynamic_Customer() {c_Name = c.Name, c_Id = c.Id})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"c_Name\":\"Nic\",\"c_Id\":1}]");
    }

    [Fact]
    public void TestWhereInScalarSelectStatement()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE Id in (1, 2)";
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
            "() => value(tests.Customer[]).Where(c => new [] {1, 2}.Any(z => (z == c.Id))).Select(Param_0 => new Dynamic_Customer() {Id = Param_0.Id, Name = Param_0.Name})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"Id\":1,\"Name\":\"Nic\"}]");
    }
}
