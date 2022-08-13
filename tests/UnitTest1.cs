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

        var desiredResult = @"
        var f = from c in _customers
                join cat in _categories on c.CategoryId equals cat.Id
                where c.State == ""MA""
                select new 
                    {
                        dbo_Customers_Id = c.Id, 
                        dbo_Customers_Name = c.Name, 
                        dbo_Categories_Name = cat.Name };";

        var f = from c in _customers
                join cat in _categories on c.CategoryId equals cat.Id
                where c.State == "MA"
                select new 
                    {
                        dbo_Customers_Id = c.Id, 
                        dbo_Customers_Name = c.Name, 
                        dbo_Categories_Name = cat.Name };
        /*
        Join<TOuter,TInner,TKey,TResult>(
            IEnumerable<TOuter>, 
            IEnumerable<TInner>, 
            Func<TOuter,TKey>, 
            Func<TInner,TKey>, 
            Func<TOuter,TInner,TResult>)
        */
        Expression<Func<IEnumerable<Customer>, IEnumerable<Category>, IEnumerable<dynamic>>> f2 = 
            (   
                IEnumerable<Customer> customers, 
                IEnumerable<Category> categories
            ) =>
            customers
                .Join<Customer, Category, int, dynamic>( 
                    categories, 
                    (Customer cust) => cust.CategoryId, 
                    (Category cat) => cat.Id, 
                    (Customer customer, Category category) => new {customer, category} );

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

    LambdaExpression CreateExpression(SqlTableExpression expression, Type elementType, IEnumerable<object> elements, string parameterName) {
        switch (expression) {
            case SqlJoinTableExpression sqlJoinStatement: 
                return null;// CreateJoinExpression(sqlJoinStatement, );
                break;
            case SqlTableRefExpression sqlTableRefStatement: 
                return CreateRefExpression(sqlTableRefStatement, elementType, elements, parameterName );
                break;
        }
        return null;
    }

    LambdaExpression? CreateJoinExpression(SqlJoinTableExpression sqlJoinStatement, Type rightMappedType, Type innerMappedType, string outerParameterName, string innerParameterName, SqlConditionClause onClause, out Type elementType) {
        
        elementType = null;
        Expression right = CreateExpression(sqlJoinStatement.Right, out Type rightElementType);
        Expression left = CreateExpression(sqlJoinStatement.Left, out Type leftElementType); 

        Type leftKeyType = null;
        Expression outerKeySelector = null;  //Func<TOuter, TKey>
        Expression innerKeySelector = null;  //Func<TInner, TKey>
        Type typeTupleOfTOuterAndTInner = typeof(Tuple<,>).MakeGenericType(rightMappedType, innerMappedType);
        elementType = typeTupleOfTOuterAndTInner;

        LambdaExpression joinSelector = null;

        switch (onClause.Expression){
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
            
                //Microsoft.SqlServer.Management.SqlParser.SqlCodeDom.SqlScalarRefExpression.SqlColumnOrPropertyRefExpression eeee;
                Expression leftExpression = null;
                
                switch (sqlComparisonBooleanExpression.Left) {
                    case SqlScalarRefExpression innerSqlScalarRefExpression: 
                    {
                        SqlMultipartIdentifier leftsqlMultipartIdentifier = innerSqlScalarRefExpression.MultipartIdentifier;
                        string innertableName = leftsqlMultipartIdentifier.Children.Last().Sql;
                        PropertyInfo leftKeyProperty = innerMappedType.GetProperty(innertableName);
                        leftKeyType = leftKeyProperty.PropertyType;

                        ParameterExpression innerParameterExpression2 = Expression.Parameter(innerMappedType, "inner");
                        MemberExpression innerMemberAccess = Expression.MakeMemberAccess(innerParameterExpression2, leftKeyProperty);
                        innerKeySelector = Expression.Lambda(innerMemberAccess, innerParameterExpression2);
                    }
                    break;
                }
                
                switch (sqlComparisonBooleanExpression.Right) {
                    case SqlScalarRefExpression outerSqlScalarRefExpression: 
                    {
                        SqlMultipartIdentifier outerSqlMultipartIdentifier = outerSqlScalarRefExpression.MultipartIdentifier;
                        string outertableName = outerSqlMultipartIdentifier.Children.Last().Sql;
                        PropertyInfo outerKeyProperty = rightMappedType.GetProperty(outertableName);
                        leftKeyType = outerKeyProperty.PropertyType;

                        ParameterExpression outerParameterExpression2 = Expression.Parameter(rightMappedType, "outer");
                        MemberExpression outerMemberAccess = Expression.MakeMemberAccess(outerParameterExpression2, outerKeyProperty);
                        outerKeySelector = Expression.Lambda(outerMemberAccess, outerParameterExpression2);
                    }
                    break;
                }
            
                ConstructorInfo? constructorInfo = 
                    typeTupleOfTOuterAndTInner
                        .GetConstructor(new Type[] {rightMappedType, innerMappedType});
                
                ParameterExpression innerParameterExpression = Expression.Parameter(innerMappedType, "inner");
                ParameterExpression outerParameterExpression = Expression.Parameter(rightMappedType, "outer");

                Expression testExpr = Expression.MemberInit(
                    Expression.New(constructorInfo, new Expression [] {outerParameterExpression, innerParameterExpression })
                );

                joinSelector = Expression.Lambda( testExpr, new [] {outerParameterExpression, innerParameterExpression} );
                /*
                SqlMultipartIdentifier leftsqlMultipartIdentifier = leftsqlScalarExpression.MultipartIdentifier;
                string lefttableName = leftsqlMultipartIdentifier.ToString().Replace($".{leftsqlMultipartIdentifier.ColumnOrPropertyName}","");
                
                SqlScalarExpression rightsqlScalarExpression = sqlComparisonBooleanExpression.Right;
                SqlMultipartIdentifier rightsqlMultipartIdentifier = rightsqlScalarExpression.MultipartIdentifier;
                string righttableName = rightsqlMultipartIdentifier.ToString().Replace($".{rightsqlMultipartIdentifier.ColumnOrPropertyName}","");
                
                PropertyInfo leftKeyProperty = innerMappedType.GetProperty(leftsqlMultipartIdentifier.ColumnOrPropertyName);
                PropertyInfo rightKeyProperty = innerMappedType.GetProperty(rightsqlMultipartIdentifier.ColumnOrPropertyName);
*/
               // sqlBooleanExpression.
            break;
        }
       
//        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
        IEnumerable<MethodInfo> joinMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Join");

        MethodInfo? joinMethodInfo = 
            joinMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 5 
                        && mi.GetParameters()[4].ParameterType.GetGenericTypeDefinition() == typeof(Func<,,>) );
        
        Type typeIEnumerableOfTOuter = typeof(IEnumerable<>).MakeGenericType(rightMappedType);
        Type typeIEnumerableOfTInner = typeof(IEnumerable<>).MakeGenericType(innerMappedType);
        //Func<TOuter, TKey>
        //Type joinKeyType = 
        Type typeFuncTakingTOuterReturningTKey = typeof(Func<,>).MakeGenericType(rightMappedType, leftKeyType);
        Type typeFuncTakingTInnerReturningTKey = typeof(Func<,>).MakeGenericType(innerMappedType, leftKeyType);

        Type typeResultSelector = typeof(Func<,,>).MakeGenericType(rightMappedType, innerMappedType, typeTupleOfTOuterAndTInner);

        ParameterExpression paramOfTypeIEnumerableOfTOuter = Expression.Parameter(typeIEnumerableOfTOuter, outerParameterName);
        ParameterExpression paramOfTypeIEnumerableOfTInner = Expression.Parameter(typeIEnumerableOfTInner, innerParameterName);

        ParameterExpression paramOfTypeTOuter = Expression.Parameter(rightMappedType, outerParameterName);
        ParameterExpression paramOfTypeTInner = Expression.Parameter(innerMappedType, innerParameterName);

        //Expression resultSelectorExpression = Expression

        //ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        // Creating an expression for the method call and specifying its parameter.
        //        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
        Type typeOfTuple =
            typeof(Tuple<,>)
                .MakeGenericType(rightMappedType, innerMappedType);

        Type typeIEnumerableOfTuple =
            typeof(IEnumerable<>)
                .MakeGenericType(typeOfTuple);

        MethodInfo joinSpecificMethodInfo = joinMethodInfo.MakeGenericMethod(new [] { rightMappedType, innerMappedType, leftKeyType, typeOfTuple });

        MethodCallExpression joinMethodCall = Expression.Call(
            method: joinSpecificMethodInfo,
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfTOuter,paramOfTypeIEnumerableOfTInner, outerKeySelector, innerKeySelector, joinSelector }
        );

        Type funcTakingIEnumerableOfCustomerAndIEnumerableOfCategoryReturningIEnumerableOfTuple =
            typeof(Func<,,>)
                .MakeGenericType(typeIEnumerableOfTOuter, typeIEnumerableOfTInner, typeIEnumerableOfTuple);

        LambdaExpression l2 = Expression.Lambda(
            funcTakingIEnumerableOfCustomerAndIEnumerableOfCategoryReturningIEnumerableOfTuple,
            joinMethodCall, new [] { paramOfTypeIEnumerableOfTOuter, paramOfTypeIEnumerableOfTInner });
        return l2;

    }

    Type? GetMappedType(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key].GetType().GetElementType();
        }
        return null;
    }
    IEnumerable<object>? GetMappedCollection(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key];
        }
        return null; 
    }
    LambdaExpression? CreateRefExpression(SqlTableRefExpression sqlTableRefExpression, Type elementType, IEnumerable<object> elementArray, string parameterName) {
        
        //Expression<Func<IEnumerable<Customer>>> customersss = () => elementArray;

        string key = sqlTableRefExpression.Sql;
        Type? mappedType = GetMappedType(key);
        if (mappedType == null)
            return null;


        var constant = Expression.Constant(elementArray);
        
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( elementType ); // == IEnumerable<mappedType>

        Type funcTakingNothingReturnsIEnumerableOfCustomer = typeof(Func<>).MakeGenericType(typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingNothingReturnsIEnumerableOfCustomer, constant, new ParameterExpression[] {});
        return l;
    }

    LambdaExpression? CreateWhereExpression(SqlTableRefExpression sqlTableRefExpression, Type mappedType, string parameterName) {
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( mappedType ); // == IEnumerable<mappedType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType, parameterName);


        ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(mappedType, typeof(bool));
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, Expression.Constant(true), selectorParam);
        
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
            method: whereMethodInfo.MakeGenericMethod(new [] { mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, selector}
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new [] {paramOfTypeIEnumerableOfMappedType});
        return l;
        //Func<IEnumerable<Customer>, IEnumerable<Customer>> transform = (Func<IEnumerable<Customer>, IEnumerable<Customer>>)l.Compile();
        //IEnumerable<Customer> transformed = transform(mappedCollection.OfType<Customer>().Cast<Customer>());

    }

    Expression CreateSelectExpression(SqlWhereClause whereClause, Type mappedType, string parameterName ) {
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( mappedType ); // == IEnumerable<mappedType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);

        ParameterExpression transformerParam = Expression.Parameter(mappedType, parameterName);
        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType(mappedType, mappedType);
        LambdaExpression transformer = Expression.Lambda(funcTakingCustomerReturningCustomer, transformerParam, transformerParam);
       
        IEnumerable<MethodInfo> selectMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Select");

        MethodInfo? selectMethodInfo = 
            selectMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );


        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression selectMethodCall = Expression.Call(
            method: selectMethodInfo.MakeGenericMethod(new [] { mappedType, mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, transformer}
        );

        ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(mappedType, typeof(bool));
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, Expression.Constant(true), selectorParam);
        return selector;
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

    public static object GetDefault(Type type)
    {
        if(type.GetTypeInfo().IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }
    
    public LambdaExpression? CreateExpression(SqlTableExpression expression, out Type elementType) { 
        elementType = null;
        switch (expression) {
            case SqlQualifiedJoinTableExpression sqlJoinStatement: 
                Type mappedOuterType = GetMappedType(sqlJoinStatement.Right.Sql);
                if (mappedOuterType == null)
                    return null;

                Type mappedInnerType = GetMappedType(sqlJoinStatement.Left.Sql);
                if (mappedInnerType == null)
                    return null;

                return CreateJoinExpression(sqlJoinStatement, mappedOuterType, mappedInnerType, "o", "i", sqlJoinStatement.OnClause, out elementType);

            case SqlTableRefExpression sqlTableRefStatement: 
                Type mappedType = GetMappedType(sqlTableRefStatement.Sql);
                if (mappedType == null)
                    return null;
                elementType = mappedType;
                IEnumerable<object> mappedCollection2 = GetMappedCollection(sqlTableRefStatement.Sql);
                return CreateRefExpression(sqlTableRefStatement, mappedType, mappedCollection2, "p");
        }
        return null;
    }

    public LambdaExpression? CreateExpression(SqlFromClause fromClause, out Type elementType) {
        elementType = null;
        var result = new List<Expression>();
        SqlTableExpression? expression = fromClause.TableExpressions.FirstOrDefault();
        if (expression == null) 
            return null;
        return CreateExpression(expression, out elementType);
    }
    
    LambdaExpression CreateWhereExpression(SqlWhereClause whereClause, Type elementType) {
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
        
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( elementType ); // == IEnumerable<elementType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);

        ParameterExpression selectorParam = Expression.Parameter(elementType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
        Expression selectorExpression = Expression.Constant(true);
        switch (whereClause.Expression) {
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
            {
                Expression leftKeySelector = null;
                Type leftKeyType = null;
                ParameterExpression parameterExpression2 = Expression.Parameter(elementType, "p");

                Expression rightKeySelector = null;
                Type rightKeyType = null;

                switch (sqlComparisonBooleanExpression.Left) {
                    case SqlScalarRefExpression leftSqlScalarRefExpression: 
                    {
                        SqlMultipartIdentifier leftsqlMultipartIdentifier = leftSqlScalarRefExpression.MultipartIdentifier;
                        string innertableName = leftsqlMultipartIdentifier.Children.Last().Sql;
                        PropertyInfo leftKeyProperty = elementType.GetProperty(innertableName);
                        leftKeyType = leftKeyProperty.PropertyType;

                        leftKeySelector = Expression.MakeMemberAccess(parameterExpression2, leftKeyProperty);
                    }
                    break;
                }
                
                switch (sqlComparisonBooleanExpression.Right) {
                    case SqlScalarRefExpression rightSqlScalarRefExpression: 
                    {
                        SqlMultipartIdentifier rightSqlMultipartIdentifier = rightSqlScalarRefExpression.MultipartIdentifier;
                        string rightcolumnName = rightSqlMultipartIdentifier.Children.Last().Sql;
                        PropertyInfo rightKeyProperty = elementType.GetProperty(rightcolumnName);
                        rightKeyType = rightKeyProperty.PropertyType;

                        rightKeySelector = Expression.MakeMemberAccess(parameterExpression2, rightKeyProperty);
                    }
                    break;

                    case SqlLiteralExpression rightSqlLiteralExpression:
                    {
                        rightKeySelector = Expression.Constant(rightSqlLiteralExpression.Value);
                    }
                    break;
                }

                switch(sqlComparisonBooleanExpression.ComparisonOperator) {
                    case SqlComparisonBooleanExpressionType.Equals: {
                        selectorExpression = Expression.MakeBinary(ExpressionType.Equal, leftKeySelector, rightKeySelector);
                        //typeof(Func<TLeft, bool>)
                        Type typeFuncTakingTElementTypeReturningBool = 
                            typeof(Func<,>)
                                .MakeGenericType(elementType, typeof(bool));
                        LambdaExpression ll = 
                            Expression
                                .Lambda(
                                    typeFuncTakingTElementTypeReturningBool,
                                    selectorExpression,
                                    new ParameterExpression[] { parameterExpression2 }
                                );
                        return ll;
                    }
                }
            }
            break;
        }
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, selectorExpression, selectorParam);

        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new [] { elementType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, selector}
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new [] {paramOfTypeIEnumerableOfMappedType});
        return l;
    }

    void ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;
        var selectClause = query.SelectClause;
        //WriteLine($"Select columns {string.Join(", ", selectClause.SelectExpressions.Select(_ => _.Sql))}");
        //Type? mappedType = null;
        //IEnumerable<object> mappedCollection = _map[];
        SqlFromClause fromClause = query.FromClause;
        List<SqlPropertyRefColumn> schema = GetSchema(query.FromClause);
        LambdaExpression fromExpression = this.CreateExpression(query.FromClause, out Type fromExpressionReturnType);
        LambdaExpression whereExpression = this.CreateWhereExpression(query.WhereClause, fromExpressionReturnType);
        var xxx = fromExpression.Compile();
        var yyy = whereExpression.Compile();
        IEnumerable<object> zzz = (IEnumerable<object>)xxx.DynamicInvoke();
        IEnumerable<object> afterWhere = zzz.Where( yy => (bool)yyy.DynamicInvoke(yy) );
        

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