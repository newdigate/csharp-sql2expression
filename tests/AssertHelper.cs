namespace tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class AssertHelper : IAssertHelper
{
    public void AssertArguments(IEnumerable<ArgumentSyntax> joinMethodCallArguments, params string[] args)
    {
        Assert.Equal(args.Count(), joinMethodCallArguments.Count());
        List<ArgumentSyntax> joinMethodCallArgumentList = joinMethodCallArguments.ToList();
        int index = 0;
        foreach (string arg in args)
        {
            Assert.Equal(arg, joinMethodCallArgumentList[index].ToFullString());
            index++;
        }
    }

    public void AssertSelectInitializers(InvocationExpressionSyntax selectInvocation, params string[] expectedInitializers)
    {
        List<AnonymousObjectMemberDeclaratorSyntax> propertyDeclarators = GetSelectMethodAnonObjectInitializers(selectInvocation);
        List<string> actualInitializers = propertyDeclarators.Select(pd => pd.ToFullString()).ToList();
        foreach (string expectedInitializer in expectedInitializers)
        {
            Assert.Contains(expectedInitializer, actualInitializers);
        }
    }

    public List<AnonymousObjectMemberDeclaratorSyntax> GetSelectMethodAnonObjectInitializers(InvocationExpressionSyntax selectInvocation)
    {
        CSharpSyntaxNode? selectArgument = (selectInvocation.ArgumentList.Arguments[0].Expression as SimpleLambdaExpressionSyntax)?.Body;
        AnonymousObjectCreationExpressionSyntax? selectObjectInitializer = selectArgument as AnonymousObjectCreationExpressionSyntax;
        List<AnonymousObjectMemberDeclaratorSyntax> propertyDeclarators = selectObjectInitializer.Initializers.ToList();
        return propertyDeclarators;
    }
}
