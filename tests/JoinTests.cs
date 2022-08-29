using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class JoinTests
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;

    public JoinTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapterFactory()
                .Create(dataSet.Map);
    }

    [Fact]
    public void TestSelectJoinStatement()
    {
        const string sql = @"
SELECT dbo.Customers.Id, dbo.Customers.Name, dbo.Categories.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.StateId = 1";
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
            "() => value(tests.Category[]).Join(value(tests.Customer[]), right => right.Id, left => left.CategoryId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories() {dbo_Customers = left, dbo_Categories = right}).Where(c => (c.dbo_Customers.StateId == 1)).Select(Param_0 => new Dynamic_Dynamic_dbo_Customers_dbo_Categories() {dbo_Customers_Id = Param_0.dbo_Customers.Id, dbo_Customers_Name = Param_0.dbo_Customers.Name, dbo_Categories_Name = Param_0.dbo_Categories.Name})", 
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\"}]");
    }

    [Fact]
    public void TestSelectDoubleJoinStatement()
    {
        const string sql = @"
SELECT dbo.Customers.Id, dbo.Customers.Name,dbo.Categories.Name, dbo.States.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
WHERE dbo.States.Name = 'MA'";
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
            "() => value(tests.State[]).Join(value(tests.Category[]).Join(value(tests.Customer[]), right => right.Id, left => left.CategoryId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States() {dbo_Customers = left, dbo_Categories = right}), right => right.Id, left => left.dbo_Customers.StateId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States() {dbo_Customers = left.dbo_Customers, dbo_Categories = left.dbo_Categories, dbo_States = right}).Where(c => (c.dbo_States.Name == \"MA\")).Select(Param_0 => new Dynamic_Dynamic_dbo_Customers_dbo_Categories_dbo_States() {dbo_Customers_Id = Param_0.dbo_Customers.Id, dbo_Customers_Name = Param_0.dbo_Customers.Name, dbo_Categories_Name = Param_0.dbo_Categories.Name, dbo_States_Name = Param_0.dbo_States.Name})",            
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\",\"dbo_States_Name\":\"MA\"}]");
    }

    [Fact]
    public void TestSelectTripleJoinStatement()
    {
        const string sql = @"
SELECT dbo.Customers.Id, dbo.Customers.Name,dbo.Categories.Name, dbo.States.Name, dbo.Brands.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
INNER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.States.Name = 'MA' and dbo.Brands.Name = 'Coke' ";
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
            "() => value(tests.Brand[]).Join(value(tests.State[]).Join(value(tests.Category[]).Join(value(tests.Customer[]), right => right.Id, left => left.CategoryId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers = left, dbo_Categories = right}), right => right.Id, left => left.dbo_Customers.StateId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers = left.dbo_Customers, dbo_Categories = left.dbo_Categories, dbo_States = right}), right => right.Id, left => left.dbo_Customers.BrandId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers = left.dbo_Customers, dbo_Categories = left.dbo_Categories, dbo_States = left.dbo_States, dbo_Brands = right}).Where(c => ((c.dbo_States.Name == \"MA\") And (c.dbo_Brands.Name == \"Coke\"))).Select(Param_0 => new Dynamic_Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers_Id = Param_0.dbo_Customers.Id, dbo_Customers_Name = Param_0.dbo_Customers.Name, dbo_Categories_Name = Param_0.dbo_Categories.Name, dbo_States_Name = Param_0.dbo_States.Name, dbo_Brands_Name = Param_0.dbo_Brands.Name})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\",\"dbo_States_Name\":\"MA\",\"dbo_Brands_Name\":\"Coke\"}]");
    }

    [Fact]
    public void TestSelectStarFromTripleJoinStatement()
    {
        const string sql = @"
SELECT *
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
INNER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.States.Name = 'MA'";

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
            "() => value(tests.Brand[]).Join(value(tests.State[]).Join(value(tests.Category[]).Join(value(tests.Customer[]), right => right.Id, left => left.CategoryId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers = left, dbo_Categories = right}), right => right.Id, left => left.dbo_Customers.StateId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers = left.dbo_Customers, dbo_Categories = left.dbo_Categories, dbo_States = right}), right => right.Id, left => left.dbo_Customers.BrandId, (right, left) => new Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {dbo_Customers = left.dbo_Customers, dbo_Categories = left.dbo_Categories, dbo_States = left.dbo_States, dbo_Brands = right}).Where(c => (c.dbo_States.Name == \"MA\")).Select(Param_0 => new Dynamic_Dynamic_dbo_Customers_dbo_Categories_dbo_States_dbo_Brands() {CategoryId = Param_0.dbo_Customers.CategoryId, StateId = Param_0.dbo_Customers.StateId, BrandId = Param_0.dbo_Customers.BrandId, Id = Param_0.dbo_Customers.Id, Name = Param_0.dbo_Customers.Name, Id2 = Param_0.dbo_Categories.Id, Name2 = Param_0.dbo_Categories.Name, Id3 = Param_0.dbo_States.Id, Name3 = Param_0.dbo_States.Name, Id4 = Param_0.dbo_Brands.Id, Name4 = Param_0.dbo_Brands.Name})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 

        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(
            "[{\"CategoryId\":1,\"StateId\":1,\"BrandId\":1,\"Id\":1,\"Name\":\"Nic\",\"Id2\":1,\"Name2\":\"Tier 1\",\"Id3\":1,\"Name3\":\"MA\",\"Id4\":1,\"Name4\":\"Coke\"}]",
            jsonResult
        );
    }
}
