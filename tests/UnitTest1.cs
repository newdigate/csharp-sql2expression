using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class UnitTest1
{
    #region static members
    private static readonly Customer[] _customers = new [] { new Customer() {Id = 1, Name="Nic", StateId=1, CategoryId=1, BrandId=1}};
    private static readonly Category[] _categories = new [] { new Category() {Id = 1, Name="Tier 1"}};
    private static readonly State[] _states = new [] { new State() { Id = 1, Name = "MA" }};
    private static readonly Brand[] _brands = new [] { new Brand() { Id = 1, Name = "Coke" }};
    #endregion

    private readonly ExpressionAdapter _expressionAdapter;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
    private readonly Dictionary<string, IEnumerable<object>> _map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers},
            { "dbo.States", _states},
            { "dbo.Brands", _brands},
            { "dbo.Categories", _categories}};

    public UnitTest1() {
        TypeMapper typeMapper = new TypeMapper(_map);

        _expressionAdapter = 
            new ExpressionAdapter(
                typeMapper, 
                new CollectionMapper(_map), 
                new SqlFieldProvider(typeMapper), 
                new FieldMappingProvider(typeMapper));

        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapter(
                _expressionAdapter);
    }

    [Fact]
    public void TestSelectStatement()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE StateId = 1";
        var parseResult = Parser.Parse(sql);
        ProcessEvaluateAndDisplayParseResult(parseResult);
    }

    [Fact]
    public void TestSelectJoinStatement()
    {
        const string sql = @"
SELECT 
    dbo.Customers.Id, 
    dbo.Customers.Name, 
    dbo.Categories.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories 
    ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.StateId = 1";
        var parseResult = Parser.Parse(sql);

        ProcessEvaluateAndDisplayParseResult(parseResult);
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
        ProcessEvaluateAndDisplayParseResult(parseResult);
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
WHERE dbo.States.Name = 'MA'";
        var parseResult = Parser.Parse(sql);
        ProcessEvaluateAndDisplayParseResult(parseResult);
    }

    private IEnumerable<object> Evaluate (LambdaExpression expression){
        Delegate finalDelegate = expression.Compile();
        IEnumerable<object> result = (IEnumerable<object>)finalDelegate.DynamicInvoke();
        return result;
    }

    private void ProcessEvaluateAndDisplayParseResult(ParseResult parseResult) {
        foreach (var batch in parseResult.Script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case SqlSelectStatement selectStatement:
                        LambdaExpression lambda = 
                            _sqlSelectStatementExpressionAdapter
                                .ProcessSelectStatement(selectStatement);
                        IEnumerable<object> result = Evaluate(lambda); 
                        WriteLine(JsonConvert.SerializeObject(result));  
                        break;
                    default:
                        WriteLine("Unsupported statment. Printing inner XML");
                        WriteLine(statement.Xml);
                        break;
                }
            }
        }
    }
}