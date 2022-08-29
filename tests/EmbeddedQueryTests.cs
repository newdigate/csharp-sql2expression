using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class EmbeddedQueryTests
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
    private readonly LambdaStringToCSharpConverter _csharpConverter; 

    public EmbeddedQueryTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        SqlSelectStatementExpressionAdapterFactory factory =  new SqlSelectStatementExpressionAdapterFactory();
        _sqlSelectStatementExpressionAdapter = 
            factory
                .Create(dataSet.Map);
        _csharpConverter = factory.CreateLambdaExpressionConverter(dataSet.Map, dataSet.InstanceMap);
    }

    [Fact]
    public void TestEmbeddedSelectStatement()
    {
        const string sql = "SELECT c.Name, c.Id FROM (SELECT Id, Name FROM dbo.Customers WHERE StateId = 1) c";
        const string expected = "_customers.Where(c => (c.StateId == 1)).Select(Param_0 => new {Id = Param_0.Id, Name = Param_0.Name}).Select(c => new {c_Name = c.Name, c_Id = c.Id})";
        
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

        Xunit.Assert.Equal(jsonResult, "[{\"c_Name\":\"Nic\",\"c_Id\":1}]");
    }

    [Fact]
    public void TestWhereInScalarSelectStatement()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE Id in (1, 2)";
        const string expected = "_customers.Where(c => new [] {1, 2}.Any(z => (z == c.Id))).Select(Param_0 => new {Id = Param_0.Id, Name = Param_0.Name})";
        
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

        Xunit.Assert.Equal(jsonResult, "[{\"Id\":1,\"Name\":\"Nic\"}]");
    }

    [Fact]
    public void TestWhereInTableExpressionStatement()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE Id in (SELECT Id from dbo.Customers)";
        const string expected = "_customers.Where(c => _customers.Select(Param_0 => Param_0.Id).Any(z => (z == c.Id))).Select(Param_1 => new {Id = Param_1.Id, Name = Param_1.Name})";

        ParseResult? parseResult = Parser.Parse(sql);
        SqlSelectStatement? selectStatement =
            parseResult?.Script.Batches
                .SelectMany( b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Xunit.Assert.NotNull(lambda);
        string csharp = _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString());
        WriteLine(csharp);
        Xunit.Assert.Equal(expected, csharp);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal("[{\"Id\":1,\"Name\":\"Nic\"}]", jsonResult);
        /*
            _customers
                .Where( 
                    c => 
                        _customers
                            .Select(Param_0 => Param_0.Id)
                            .Any(z => (z == c.Id)))
                .Select(Param_1 => 
                    new {Id = Param_1.Id, Name = Param_1.Name});
        */
    }
}
