using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class AliasTests
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
    private readonly LambdaStringToCSharpConverter _csharpConverter; 

    public AliasTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        SqlSelectStatementExpressionAdapterFactory factory =  new SqlSelectStatementExpressionAdapterFactory();
        _sqlSelectStatementExpressionAdapter = 
            factory
                .Create(dataSet.Map);
        _csharpConverter = factory.CreateLambdaExpressionConverter(dataSet.Map, dataSet.InstanceMap);

    }

    [Fact]
    public void TestSelectColumnAliasStatement()
    {
        const string sql = "SELECT Id as CustomerId, Name as CustomerName FROM dbo.Customers WHERE StateId = 1";
        const string expected = "_customers.Where(c => (c.StateId == 1)).Select(Param_0 => new {CustomerId = Param_0.Id, CustomerName = Param_0.Name})";
        
        ParseResult? parseResult = Parser.Parse(sql);
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
        Xunit.Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"CustomerId\":1,\"CustomerName\":\"Nic\"}]");
    }

    [Fact]
    public void TestSelectTableAliasStatement()
    {
        const string sql = "SELECT c.Id as CustomerId, c.Name as CustomerName FROM dbo.Customers c WHERE c.StateId = 1";
        const string expected =  "_customers.Where(c => (c.StateId == 1)).Select(c => new {CustomerId = c.Id, CustomerName = c.Name})";
        
        ParseResult parseResult = Parser.Parse(sql);
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
        Xunit.Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"CustomerId\":1,\"CustomerName\":\"Nic\"}]");
    }
}
