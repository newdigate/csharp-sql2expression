using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.SqlServer.Management.SqlParser;
using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System.Collections;
using static System.Diagnostics.Debug;

namespace tests;

public class UnitTest1
{
    public class Customer {
        public int Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public int CategoryId { get; set; }
    }

    public class Category {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private static readonly Customer[] _customers = new [] { new Customer() {Id = 1, Name="Nic", State="MA", CategoryId=1}};
    private static readonly Category[] _categories = new [] { new Category() {Id = 1, Name="Tier 1"}};

 
    private readonly Dictionary<string, IEnumerable<object>> _map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers},
            { "dbo.Categories", _categories}};



    [Fact]
    public void Test1()
    {
        var parseResult = Parser.Parse("SELECT Id, Name FROM dbo.Customers WHERE State = 'MA'");
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
        var parseResult = Parser.Parse(@"
        SELECT 
            dbo.Customers.Id, 
            dbo.Customers.Name, 
            dbo.Categories.Name
        FROM dbo.Customers 
        INNER JOIN dbo.Categories 
            ON dbo.Customers.CategoryId = dbo.Categories.Id
        WHERE Customers.State = 'MA'");
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

    List<string> GetSchema(SqlFromClause clause) {
        var result = new List<string>();
        foreach(SqlTableExpression expression in clause.TableExpressions) {
            result.AddRange(GetSqlTableExpressionSchema(expression));
        }
        return result;
    }

    List<string> GetSqlTableExpressionSchema(SqlTableExpression expression) {
        var result = new List<string>();
        switch (expression) {
            case SqlJoinTableExpression sqlJoinStatement: 
                result.AddRange( GetJoinSchema(sqlJoinStatement) );
                break;
            case SqlTableRefExpression sqlTableRefStatement: 
                result.AddRange( GetSqlTableRefExpressionSchema(sqlTableRefStatement) );
                break;
        }
        return result;
    }

    List<string> GetJoinSchema(SqlJoinTableExpression sqlJoinStatement) {
        var result = new List<string>();
        result.AddRange(GetSqlTableExpressionSchema(sqlJoinStatement.Left));
        result.AddRange(GetSqlTableExpressionSchema(sqlJoinStatement.Right));
        return result;
    }

    List<string> GetSqlTableRefExpressionSchema(SqlTableRefExpression sqlTableRefExpression) {
        Type? mappedType = null;
        if (_map.ContainsKey(sqlTableRefExpression.Sql)) {
            mappedType = _map[sqlTableRefExpression.Sql].GetType().GetElementType();
            return mappedType.GetProperties().Select( p => $"{sqlTableRefExpression.Sql}.{p.Name}").ToList();
        }
        return new List<string>();
    }

    void ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;
        var selectClause = query.SelectClause;
        //WriteLine($"Select columns {string.Join(", ", selectClause.SelectExpressions.Select(_ => _.Sql))}");
        Type? mappedType = null;
        IEnumerable<object> mappedCollection = null;
        SqlFromClause fromClause = query.FromClause;
        List<string> schema = GetSchema(query.FromClause);
        WriteLine($"columns \t\t{string.Join(", \r\n\t\t", schema)}");



        /*
        Expression<Func<IEnumerable<dynamic>>> e = 
                        () => 
                            _customers
                                .Where(
                                    c => c.State == "MA")
                                .Select( 
                                    c => 
                                        new { c.Id, c.Name }); */
        Expression<Func<IEnumerable<Customer>>> e = () => _customers;

        WriteLine($"from tables {string.Join(", ", fromClause.TableExpressions.Select(_ => _.Sql))}");
        
        MemberInfo? memberInfo = GetType().GetMember(nameof(_customers), BindingFlags.NonPublic | BindingFlags.Static).ToList().FirstOrDefault();
        MemberExpression memberAccess = Expression.MakeMemberAccess(null, memberInfo);

        //public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector);
        IEnumerable<MethodInfo> selectMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Select");

        //public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
        IEnumerable<MethodInfo> whereMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Where");

        MethodInfo? selectMethodInfo = 
            selectMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );

        MethodInfo? whereMethodInfo = 
            whereMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );
               
        //Lambda whereLambda = Expression.Lambda();

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( mappedType ); // == IEnumerable<mappedType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);

        ParameterExpression transformerParam = Expression.Parameter(mappedType, "t");
        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType(mappedType, mappedType);
        LambdaExpression transformer = Expression.Lambda(funcTakingCustomerReturningCustomer, transformerParam, transformerParam);
       
        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression selectMethodCall = Expression.Call(
            method: selectMethodInfo.MakeGenericMethod(new [] { mappedType, mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, transformer}
        );

        ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(mappedType, typeof(bool));
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, Expression.Constant(true), selectorParam);
        
        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new [] { mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, selector}
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new [] {paramOfTypeIEnumerableOfMappedType});
        
        Func<IEnumerable<Customer>, IEnumerable<Customer>> transform = (Func<IEnumerable<Customer>, IEnumerable<Customer>>)l.Compile();
        IEnumerable<Customer> transformed = transform(mappedCollection.OfType<Customer>().Cast<Customer>());

        var whereClause = query.WhereClause;
        WriteLine($"where {whereClause.Expression.Sql}");
    }
}