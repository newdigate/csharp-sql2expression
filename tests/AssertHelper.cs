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

    public void AssertInitializers(ExpressionSyntax selectInvocation, params string[] expectedInitializers)
    {
        List<AnonymousObjectMemberDeclaratorSyntax>? propertyDeclarators = null;
        switch (selectInvocation) {
            case InvocationExpressionSyntax invocationExpressionSyntax: 
                propertyDeclarators = GetSelectMethodAnonObjectInitializers(invocationExpressionSyntax); 
                break;
            case AnonymousObjectCreationExpressionSyntax anonymousObjectCreationExpressionSyntax:
                propertyDeclarators = GetSelectMethodAnonObjectInitializers(anonymousObjectCreationExpressionSyntax); 
                break;
            default:
                throw new Exception($"Test is not sure how to get initializers for {selectInvocation.GetType()}");
        }
        
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
        return GetSelectMethodAnonObjectInitializers(selectObjectInitializer);
    }

    public List<AnonymousObjectMemberDeclaratorSyntax> GetSelectMethodAnonObjectInitializers(AnonymousObjectCreationExpressionSyntax anonymousObjectCreationExpressionSyntax)
    {
        List<AnonymousObjectMemberDeclaratorSyntax> propertyDeclarators = anonymousObjectCreationExpressionSyntax.Initializers.ToList();
        return propertyDeclarators;
    }
}
