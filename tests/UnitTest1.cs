using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;

public class UnitTest1
{
    private readonly ExpressionAdapter _expressionAdapter;

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

    public class SqlPropertyRefColumn {
        private readonly PropertyInfo _property;
        private readonly SqlTableRefExpression _sqlTableRefExpression;
        private readonly Type _mappedType;

        public SqlPropertyRefColumn(PropertyInfo property, SqlTableRefExpression sqlTableRefExpression, Type mappedType)
        {
            _property = property;
            _sqlTableRefExpression = sqlTableRefExpression;
            _mappedType = mappedType;
        }

        public PropertyInfo Property { get { return _property; } }
        public SqlTableRefExpression SqlTableRefExpression { get { return _sqlTableRefExpression; } }
        public Type MappedType { get { return _mappedType; } }

    }

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

    List<SqlPropertyRefColumn> GetSchema(SqlFromClause clause) {
        var result = new List<SqlPropertyRefColumn>();
        foreach(SqlTableExpression expression in clause.TableExpressions) {
            result.AddRange(GetSqlTableExpressionSchema(expression));
        }
        return result;
    }


    #region column stuff
    List<SqlPropertyRefColumn> GetSqlTableExpressionSchema(SqlTableExpression expression) {
        var result = new List<SqlPropertyRefColumn>();
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

    List<SqlPropertyRefColumn> GetJoinSchema(SqlJoinTableExpression sqlJoinStatement) {
        var result = new List<SqlPropertyRefColumn>();
        result.AddRange(GetSqlTableExpressionSchema(sqlJoinStatement.Left));
        result.AddRange(GetSqlTableExpressionSchema(sqlJoinStatement.Right));
        return result;
    }

    List<SqlPropertyRefColumn> GetSqlTableRefExpressionSchema(SqlTableRefExpression sqlTableRefExpression) {
        Type? mappedType = null;
        if (_map.ContainsKey(sqlTableRefExpression.Sql)) {
            mappedType = _map[sqlTableRefExpression.Sql].GetType().GetElementType();
            return 
                mappedType
                    .GetProperties()
                    .Select( 
                        p => 
                            new SqlPropertyRefColumn(p, sqlTableRefExpression, mappedType ))
                    .ToList();
        }
        return new List<SqlPropertyRefColumn>();
    }
    #endregion



    
    void ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;
        var selectClause = query.SelectClause;
        //WriteLine($"Select columns {string.Join(", ", selectClause.SelectExpressions.Select(_ => _.Sql))}");
        //Type? mappedType = null;
        //IEnumerable<object> mappedCollection = _map[];
        SqlFromClause fromClause = query.FromClause;
        List<SqlPropertyRefColumn> schema = GetSchema(query.FromClause);
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

        //Type typeFuncTakesNothingReturnsIEnumerableOfDynamicType = typeof(Func<>).MakeGenericType(outputType);
        //LambdaExpression result = Expression.Lambda(typeFuncTakesNothingReturnsIEnumerableOfDynamicType, )
        //var zzz2 = yyy.Invoke();
       // foreach (var cxx in zzz) {

//        }

        Dictionary<Type, List<SqlPropertyRefColumn>>  columnsForWhereClausesGrouped = new Dictionary<Type, List<SqlPropertyRefColumn>>();
        foreach (SqlPropertyRefColumn sqlPropertyRefColumn in schema) {
            List<SqlPropertyRefColumn>? columnListForGroup = null;
            if (columnsForWhereClausesGrouped.ContainsKey(sqlPropertyRefColumn.MappedType)) 
            {
                columnListForGroup = columnsForWhereClausesGrouped[sqlPropertyRefColumn.MappedType];
            } else
            {
                columnListForGroup = new List<SqlPropertyRefColumn>();
                columnsForWhereClausesGrouped[sqlPropertyRefColumn.MappedType] = columnListForGroup;
            }
            columnListForGroup.Add(sqlPropertyRefColumn);
        }


/*
        dynamic resultType = new {  };
        var annotate = (SqlPropertyRefColumn s) => $"{s.SqlTableRefExpression.Sql}.{s.Property.Name}";
        foreach (SqlPropertyRefColumn sqlPropertyRefColumn in schema) {
            resultType[annotate(sqlPropertyRefColumn).Replace(".","_")] = GetDefault(sqlPropertyRefColumn.Property.PropertyType);
        }
        mappedType = resultType.GetType();
        */
        string columnString = string.Join(", \r\n\t\t", schema.Select( s => $"{s.SqlTableRefExpression.Sql}.{s.Property.Name}"));
        WriteLine($"columns \t\t{columnString}");
        /*
        Expression<Func<IEnumerable<dynamic>>> e = 
                        () => 
                            _customers
                                .Where(
                                    c => c.State == "MA")
                                .Select( 
                                    c => 
                                        new { c.Id, c.Name }); */
       // Expression<Func<IEnumerable<Customer>>> e = () => _customers;

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
        /*
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
*/
        var whereClause = query.WhereClause;
        WriteLine($"where {whereClause.Expression.Sql}");
    }
}

public class CollectionMapper {
    private readonly Dictionary<string, IEnumerable<object>> _map;

    public CollectionMapper(Dictionary<string, IEnumerable<object>> map)
    {
        _map = map;
    }

    public IEnumerable<object>? GetMappedCollection(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key];
        }
        return null; 
    }
}
