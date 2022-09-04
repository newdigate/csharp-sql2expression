using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using Newtonsoft.Json;
namespace tests;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using src;

public class JoinTests
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
    private readonly LambdaStringToCSharpConverter _csharpConverter; 
    private readonly ICSharpCompilationProvider _csharpCompilationProvider;

    public JoinTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        SqlSelectStatementExpressionAdapterFactory factory =  new SqlSelectStatementExpressionAdapterFactory();
        _sqlSelectStatementExpressionAdapter = 
            factory
                .Create(dataSet.Map);
        _csharpConverter = factory.CreateLambdaExpressionConverter(dataSet.Map, dataSet.InstanceMap);

        string assemlyLoc = typeof(System.Linq.Enumerable).GetTypeInfo().Assembly.Location;
        DirectoryInfo? coreDir = Directory.GetParent(assemlyLoc);
        if (coreDir == null)
            throw new ApplicationException("Unable to locate core framework directory...");
        MetadataReference[] defaultReferences = 
            new[] { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(NuGet.Frameworks.CompatibilityTable).Assembly.Location),
                MetadataReference.CreateFromFile(coreDir.FullName + Path.DirectorySeparatorChar + "System.Runtime.dll"),
            };
        _csharpCompilationProvider = new CSharpCompilationProvider(defaultReferences);
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
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.StateId = 1";
        const string expected = "_customers.Join(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).Where(c => (c.dbo_Customers.StateId == 1)).Select(Param_0 => new {dbo_Customers_Id = Param_0.dbo_Customers.Id, dbo_Customers_Name = Param_0.dbo_Customers.Name, dbo_Categories_Name = Param_0.dbo_Categories.Name})";

        SqlSelectStatement? selectStatement = GetSingleSqlSelectStatement(sql);

        LambdaExpression? lambda = selectStatement != null ?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Assert.NotNull(lambda);
        string csharpString = _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString());

        #region boiler-plate
        string csharpClass = $@"
using System.Linq;
public class Brand {{
    public int Id {{ get; set; }}
    public string Name {{ get; set; }}
}}
public class Category {{
    public int Id {{ get; set; }}
    public string Name {{ get; set; }}
}}

public class Customer {{
    public int Id {{ get; set; }}
    public string Name {{ get; set; }}
    public int StateId {{ get; set; }}
    public int CategoryId {{ get; set; }}
    public int BrandId {{get; set;}}
}}

public class State {{
    public int Id {{ get; set; }}
    public string Name {{ get; set; }}
}}

public static class TestClass {{
    public static readonly Customer[] _customers = new Customer[] {{}};
    public static readonly Category[] _categories = new Category[] {{}};
    public static readonly State[] _states = new State[] {{}};
    public static readonly Brand[] _brands = new Brand[] {{}};

    public static void TestExpression() {{
        var x = {csharpString};
    }}
}}
";
        #endregion

        CSharpCompilation compilation =
            _csharpCompilationProvider
                .CompileCSharp(
                    new string[] { csharpClass },
                    out IDictionary<SyntaxTree, CompilationUnitSyntax> trees);

        InvocationExpressionSyntax invocation = GetInvocationSyntax(trees);
        List<InvocationExpressionSyntax> chainedInvocations = GetChainedInvokations(invocation);

        InvocationExpressionSyntax selectInvocation = chainedInvocations[0];
        MemberAccessExpressionSyntax firstMethod = (MemberAccessExpressionSyntax)selectInvocation.Expression;
        Assert.Equal("Select", firstMethod.Name.ToFullString());

        List<AnonymousObjectMemberDeclaratorSyntax> propertyDeclarators = GetSelectMethodAnonObjectInitializers(selectInvocation);
        List<string> actualInitializers = propertyDeclarators.Select(pd => pd.ToFullString()).ToList();
        Assert.Contains("dbo_Customers_Id = Param_0.dbo_Customers.Id", actualInitializers);
        Assert.Contains("dbo_Customers_Name = Param_0.dbo_Customers.Name", actualInitializers);
        Assert.Contains("dbo_Categories_Name = Param_0.dbo_Categories.Name", actualInitializers);

        InvocationExpressionSyntax whereInvocation = chainedInvocations[1];
        MemberAccessExpressionSyntax whereMethod = (MemberAccessExpressionSyntax)whereInvocation.Expression;
        Assert.Equal("Where", whereMethod.Name.ToFullString());

        IEnumerable<ArgumentSyntax> whereMethodCallArguments = whereInvocation.ArgumentList.Arguments;
        Assert.Equal(1, whereMethodCallArguments.Count());

        List<string> whereMethodCallArgsToString = whereMethodCallArguments.Select(a => a.ToFullString()).ToList();
        Assert.Equal("c => (c.dbo_Customers.StateId == 1)", whereMethodCallArgsToString[0]);

        InvocationExpressionSyntax joinInvocation = chainedInvocations[2];
        MemberAccessExpressionSyntax? joinMethod = (MemberAccessExpressionSyntax)joinInvocation.Expression;
        Assert.NotNull(joinMethod);
        Assert.Equal("Join", joinMethod.Name.ToFullString());

        IEnumerable<ArgumentSyntax> joinMethodCallArguments = joinInvocation.ArgumentList.Arguments;
        Assert.Equal(4, joinMethodCallArguments.Count());

        List<string> joinMethodCallArgsToString = joinMethodCallArguments.Select(a => a.ToFullString()).ToList();
        Assert.Equal("_categories", joinMethodCallArgsToString[0]);
        Assert.Equal("outer => outer.CategoryId", joinMethodCallArgsToString[1]);
        Assert.Equal("inner => inner.Id", joinMethodCallArgsToString[2]);
        Assert.Equal("(outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}", joinMethodCallArgsToString[3]);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);
        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);

        Assert.Equal(jsonResult, "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\"}]");
    }

    private static SqlSelectStatement? GetSingleSqlSelectStatement(string sql)
    {
        var parseResult = Parser.Parse(sql);
        SqlSelectStatement? selectStatement =
            parseResult.Script.Batches
                .SelectMany(b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();
        return selectStatement;
    }

    private static List<AnonymousObjectMemberDeclaratorSyntax> GetSelectMethodAnonObjectInitializers(InvocationExpressionSyntax selectInvocation)
    {
        CSharpSyntaxNode? selectArgument = (selectInvocation.ArgumentList.Arguments[0].Expression as SimpleLambdaExpressionSyntax)?.Body;
        AnonymousObjectCreationExpressionSyntax? selectObjectInitializer = selectArgument as AnonymousObjectCreationExpressionSyntax;
        List<AnonymousObjectMemberDeclaratorSyntax> propertyDeclarators = selectObjectInitializer.Initializers.ToList();
        return propertyDeclarators;
    }

    private List<InvocationExpressionSyntax> GetChainedInvokations(ExpressionSyntax expression)
    {
        List<InvocationExpressionSyntax> result = new List<InvocationExpressionSyntax>();
        ExpressionSyntax? current = expression;
        while (current != null) {
            if (current is InvocationExpressionSyntax invocationExpressionSyntax) {
                result.Add(invocationExpressionSyntax);
                if (invocationExpressionSyntax.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax) {
                    current = memberAccessExpressionSyntax.Expression;
                }
                else current = null;
            } else current = null;
        }
        return result;
    }

    private static InvocationExpressionSyntax GetInvocationSyntax(IDictionary<SyntaxTree, CompilationUnitSyntax> trees)
    {
        LocalDeclarationStatementSyntax varx = GetLocalDeclarationStatement(trees);

        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)varx.Declaration.Variables.First().Initializer.Value;
        return invocation;
    }

    private static LocalDeclarationStatementSyntax GetLocalDeclarationStatement(IDictionary<SyntaxTree, CompilationUnitSyntax> trees)
    {
        MethodDeclarationSyntax method =
            trees
                .Values
                .First()
                .SyntaxTree
                .GetCompilationUnitRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .First();

        LocalDeclarationStatementSyntax varx = method.Body.Statements.OfType<LocalDeclarationStatementSyntax>().First();
        return varx;
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
        const string expected = "_customers.Join(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).Join(_states, outer => outer.dbo_Customers.StateId, inner => inner.Id, (outer, inner) => new {dbo_States = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories}).Where(c => (c.dbo_States.Name == \"MA\")).Select(Param_0 => new {dbo_Customers_Id = Param_0.dbo_Customers.Id, dbo_Customers_Name = Param_0.dbo_Customers.Name, dbo_Categories_Name = Param_0.dbo_Categories.Name, dbo_States_Name = Param_0.dbo_States.Name})";
        
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

        Assert.NotNull(lambda);
        Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);  

        Assert.Equal(
            "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\",\"dbo_States_Name\":\"MA\"}]",
            jsonResult);
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
        const string expected = "_customers.Join(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).Join(_states, outer => outer.dbo_Customers.StateId, inner => inner.Id, (outer, inner) => new {dbo_States = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories}).Join(_brands, outer => outer.dbo_Customers.BrandId, inner => inner.Id, (outer, inner) => new {dbo_Brands = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories, dbo_States = outer.dbo_States}).Where(c => ((c.dbo_States.Name == \"MA\") And (c.dbo_Brands.Name == \"Coke\"))).Select(Param_0 => new {dbo_Customers_Id = Param_0.dbo_Customers.Id, dbo_Customers_Name = Param_0.dbo_Customers.Name, dbo_Categories_Name = Param_0.dbo_Categories.Name, dbo_States_Name = Param_0.dbo_States.Name, dbo_Brands_Name = Param_0.dbo_Brands.Name})";
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

        Assert.NotNull(lambda);
        Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);  

        Assert.Equal(jsonResult, "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\",\"dbo_States_Name\":\"MA\",\"dbo_Brands_Name\":\"Coke\"}]");
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
        const string expected = "_customers.Join(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).Join(_states, outer => outer.dbo_Customers.StateId, inner => inner.Id, (outer, inner) => new {dbo_States = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories}).Join(_brands, outer => outer.dbo_Customers.BrandId, inner => inner.Id, (outer, inner) => new {dbo_Brands = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories, dbo_States = outer.dbo_States}).Where(c => (c.dbo_States.Name == \"MA\")).Select(Param_0 => new {Id = Param_0.dbo_Categories.Id, Name = Param_0.dbo_Categories.Name, CategoryId = Param_0.dbo_Customers.CategoryId, StateId = Param_0.dbo_Customers.StateId, BrandId = Param_0.dbo_Customers.BrandId, Id2 = Param_0.dbo_Customers.Id, Name2 = Param_0.dbo_Customers.Name, Id3 = Param_0.dbo_States.Id, Name3 = Param_0.dbo_States.Name, Id4 = Param_0.dbo_Brands.Id, Name4 = Param_0.dbo_Brands.Name})";
        ParseResult? parseResult = Parser.Parse(sql);
        SqlSelectStatement? selectStatement =
            parseResult?.Script?.Batches?
                .SelectMany( b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Assert.NotNull(lambda);
        Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);        
        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);  

        Assert.Equal(
            "[{\"CategoryId\":1,\"StateId\":1,\"BrandId\":1,\"Id\":1,\"Name\":\"Nic\",\"Id2\":1,\"Name2\":\"Tier 1\",\"Id3\":1,\"Name3\":\"MA\",\"Id4\":1,\"Name4\":\"Coke\"}]",
            jsonResult
        );
    }

    [Fact]
    public void TestSelectStarFromLeftOuterTripleJoinStatement()
    {
        const string sql = @"
SELECT *
FROM dbo.Customers 
LEFT OUTER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.Name = 'Nic'";
        const string expected = "_customers.GroupJoin(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).SelectMany(x => x.dbo_Categories.DefaultIfEmpty(), (oo, ii) => new {dbo_Customers = oo.dbo_Customers, dbo_Categories = ii}).Where(c => (c.dbo_Customers.Name == \"Nic\")).Select(Param_0 => new {Id = Param_0.dbo_Customers.Id, Name = Param_0.dbo_Customers.Name, StateId = Param_0.dbo_Customers.StateId, CategoryId = Param_0.dbo_Customers.CategoryId, BrandId = Param_0.dbo_Customers.BrandId, Id2 = Param_0.dbo_Categories.Id, Name2 = Param_0.dbo_Categories.Name})";
        
        ParseResult? parseResult = Parser.Parse(sql);
        SqlSelectStatement? selectStatement =
            parseResult?.Script?.Batches?
                .SelectMany( b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Assert.NotNull(lambda);
        Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);        
        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);  

        Assert.Equal(
            "[{\"Id\":1,\"Name\":\"Nic\",\"StateId\":1,\"CategoryId\":1,\"BrandId\":1,\"Id2\":1,\"Name2\":\"Tier 1\"}]",
            jsonResult
        );

        #region for-reference
        
        Customer[] _customers  = {};
        Category[] _categories  = {};
        
        Expression<Func<IEnumerable<dynamic>>> x = () =>
            from customer in _customers
                join cat in _categories on customer.CategoryId equals cat.Id into catjoin
                from category in catjoin.DefaultIfEmpty()
                select new { category, customer };
        var xx = 
            _customers
                .GroupJoin(
                    _categories, 
                    customer => customer.CategoryId, 
                    cat => cat.Id, 
                    (customer, catjoin) => new {customer = customer, catjoin = catjoin} )
                .SelectMany( 
                    x => x.catjoin.DefaultIfEmpty(), 
                    (x, category) => new {category = category, customer = x.customer});
        
        #endregion

    }
}
