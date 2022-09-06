namespace tests;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public interface IAssertHelper
{
    void AssertArguments(IEnumerable<ArgumentSyntax> joinMethodCallArguments, params string[] args);
    void AssertInitializers(ExpressionSyntax invocation, params string[] expectedInitializers);
    List<AnonymousObjectMemberDeclaratorSyntax> GetSelectMethodAnonObjectInitializers(InvocationExpressionSyntax selectInvocation);
    void AssertDynamicProperties(object v, Dictionary<string, object> dictionary);
}
