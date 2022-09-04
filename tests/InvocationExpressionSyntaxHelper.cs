namespace tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class InvocationExpressionSyntaxHelper : IInvocationExpressionSyntaxHelper
{
    private readonly ICSharpCompilationProvider _csharpCompilationProvider;
    private readonly ITestHelper _testHelper;

    public InvocationExpressionSyntaxHelper(ICSharpCompilationProvider csharpCompilationProvider, ITestHelper testHelper)
    {
        _csharpCompilationProvider = csharpCompilationProvider;
        _testHelper = testHelper;
    }

    public InvocationExpressionSyntax GetInvocationExpressionSyntax(string csharpString)
    {
        #region boiler-plate
        string csharpClass = $@"
using System.Linq;
using tests;
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

        InvocationExpressionSyntax invocation = _testHelper.GetInvocationSyntax(trees);
        return invocation;
    }

}
