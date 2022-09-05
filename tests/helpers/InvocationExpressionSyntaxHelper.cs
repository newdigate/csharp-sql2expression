namespace tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class InvocationExpressionSyntaxHelper : IInvocationExpressionSyntaxHelper
{
    private readonly ICSharpCompilationProvider _csharpCompilationProvider;
    private readonly ITestHelper _testHelper;
    private readonly LambdaStringToCSharpConverter _csharpConverter;

    public InvocationExpressionSyntaxHelper(ICSharpCompilationProvider csharpCompilationProvider, ITestHelper testHelper, LambdaStringToCSharpConverter csharpConverter)
    {
        _csharpCompilationProvider = csharpCompilationProvider;
        _testHelper = testHelper;
        _csharpConverter = csharpConverter;
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


    private List<InvocationExpressionSyntax> GetChainedInvokations(ExpressionSyntax expression)
    {
        List<InvocationExpressionSyntax> result = new List<InvocationExpressionSyntax>();
        ExpressionSyntax? current = expression;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax invocationExpressionSyntax)
            {
                result.Add(invocationExpressionSyntax);
                if (invocationExpressionSyntax.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
                {
                    current = memberAccessExpressionSyntax.Expression;
                }
                else current = null;
            }
            else current = null;
        }
        return result;
    }


    public List<InvocationExpressionSyntax> GetChainedInvokations(string rawExpression)
    {
        InvocationExpressionSyntax invocationExpressionSyntax = 
                GetInvocationExpressionSyntax(
                    _csharpConverter
                        .ConvertLambdaStringToCSharp(rawExpression));
        return GetChainedInvokations(invocationExpressionSyntax);
    }
}
