namespace tests;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public interface IInvocationExpressionSyntaxHelper
{
    InvocationExpressionSyntax GetInvocationExpressionSyntax(string csharpString);
    List<InvocationExpressionSyntax> GetChainedInvokations(string rawExpression);
}
