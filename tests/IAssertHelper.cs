namespace tests;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public interface IAssertHelper
{
    void AssertArguments(IEnumerable<ArgumentSyntax> joinMethodCallArguments, params string[] args);
    void AssertSelectInitializers(InvocationExpressionSyntax selectInvocation, params string[] expectedInitializers);
    List<AnonymousObjectMemberDeclaratorSyntax> GetSelectMethodAnonObjectInitializers(InvocationExpressionSyntax selectInvocation);
}
