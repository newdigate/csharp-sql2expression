using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class UnitTest1
{
    private readonly ExpressionAdapter _expressionAdapter;
    private static readonly Customer[] _customers = new [] { new Customer() {Id = 1, Name="Nic", State="MA", CategoryId=1}};
    private static readonly Category[] _categories = new [] { new Category() {Id = 1, Name="Tier 1"}};

    public UnitTest1() {
        TypeMapper typeMapper = new TypeMapper(_map);
        _expressionAdapter = 
            new ExpressionAdapter(
                typeMapper, 
                new CollectionMapper(_map), 
                new SqlFieldProvider(typeMapper), 
                new FieldMappingProvider(typeMapper));
    }

    private readonly Dictionary<string, IEnumerable<object>> _map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers},
            { "dbo.Categories", _categories}};

    [Fact]
    public void Test1()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE State = 'MA'";
        var parseResult = Parser.Parse(sql);
        foreach (var batch in parseResult.Script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case SqlSelectStatement selectStatement:
                        ProcessSelectStatement(selectStatement);
                        break;
                    default:
                        WriteLine("Unsupported statment. Printing inner XML");
                        WriteLine(statement.Xml);
                        break;
                }
            }
        }
    }

    [Fact]
    public void Test2()
    {
        const string sql = @"
SELECT 
    dbo.Customers.Id, 
    dbo.Customers.Name, 
    dbo.Categories.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories 
    ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.State = 'MA'";
        var parseResult = Parser.Parse(sql);

        foreach (var batch in parseResult.Script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case SqlSelectStatement selectStatement:
                        ProcessSelectStatement(selectStatement);
                        break;
                    default:
                        WriteLine("Unsupported statment. Printing inner XML");
                        WriteLine(statement.Xml);
                        break;
                }
            }
        }
    }

    void ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;

        LambdaExpression fromExpression = _expressionAdapter.CreateExpression(query.FromClause, out Type fromExpressionReturnType);
        LambdaExpression whereExpression = _expressionAdapter.CreateWhereExpression(query.WhereClause, fromExpressionReturnType);
        LambdaExpression selectExpression = _expressionAdapter.CreateSelectExpression(query.SelectClause, fromExpressionReturnType, "sss", out Type? outputType);

        var xxx = fromExpression.Compile();
        var yyy = whereExpression.Compile();
        var sss = selectExpression.Compile();

        IEnumerable<object> zzz = (IEnumerable<object>)xxx.DynamicInvoke();
        IEnumerable<object> afterWhere = zzz.Where( yy => (bool)yyy.DynamicInvoke(yy) );
        //WriteLine(sss.ToString());
        WriteLine(fromExpression.ToString());
        WriteLine(whereExpression.ToString());
        WriteLine(selectExpression.ToString());
        var afterSelect = sss.DynamicInvoke( afterWhere.ToList() );
        WriteLine(JsonConvert.SerializeObject(afterSelect));
    }
}
