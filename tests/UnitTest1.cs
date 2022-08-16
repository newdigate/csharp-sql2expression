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
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
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

        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapter(
                _expressionAdapter);
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

    private IEnumerable<object> Evaluate (LambdaExpression expression){
        Delegate finalDelegate = expression.Compile();
        IEnumerable<object> result = (IEnumerable<object>)finalDelegate.DynamicInvoke();
        return result;
    }


    [Fact]
    public void TestAssignment() {
        IEnumerable<object> enumObjects = new List<Category>();
    }

    /*
    [Fact]
    public void TestAssignment2() {
        IEnumerable<object> enumObjects = new List<int>();
    }
    */
}

public class SqlSelectStatementExpressionAdapter {
    private readonly ExpressionAdapter _expressionAdapter;

    public SqlSelectStatementExpressionAdapter(ExpressionAdapter expressionAdapter)
    {
        _expressionAdapter = expressionAdapter;
    }

    public LambdaExpression ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;

        LambdaExpression fromExpression = _expressionAdapter.CreateExpression(query.FromClause, out Type fromExpressionReturnType);
        LambdaExpression whereExpression = _expressionAdapter.CreateWhereExpression(query.WhereClause, fromExpressionReturnType);
        LambdaExpression selectExpression = _expressionAdapter.CreateSelectExpression(query.SelectClause, fromExpressionReturnType, "sss", out Type? outputType);

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( fromExpressionReturnType ); // == IEnumerable<mappedType>

        ParameterExpression selectorParam = Expression.Parameter(fromExpressionReturnType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(fromExpressionReturnType, typeof(bool));
        
        //public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
        IEnumerable<MethodInfo> whereMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Where");

        MethodInfo? whereMethodInfo = 
            whereMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );
               

        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new [] { fromExpressionReturnType }),
            instance: null, 
            arguments: new Expression[] {
                fromExpression.Body, 
                whereExpression}
        );

        Expression finalExpression = 
            Expression
                .Invoke( 
                    selectExpression, 
                    whereMethodCall ); 

        Type typeIEnumerableOfTOutputType = typeof(IEnumerable<>).MakeGenericType( outputType ); // == IEnumerable<mappedType>
        Type typeFuncTakesNothingReturnsIEnumerableOfTOutputType = 
            typeof(Func<>)
                .MakeGenericType(typeIEnumerableOfTOutputType);

        LambdaExpression finalLambda = 
            Expression
                .Lambda(
                    typeFuncTakesNothingReturnsIEnumerableOfTOutputType,
                    finalExpression);
        return finalLambda;
        /*
        WriteLine(fromExpression.ToString());
        WriteLine(whereExpression.ToString());
        WriteLine(selectExpression.ToString());
        */
    }

}