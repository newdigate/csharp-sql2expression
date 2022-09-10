namespace tests;

using System.Reflection;
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

    public void AssertDynamicProperties(object item, Dictionary<string, object> dictionary)
    {
        foreach (PropertyInfo prop in item.GetType().GetProperties()) {
            object? itemProperty = prop.GetValue(item);

            System.Runtime.Serialization.DataMemberAttribute? dataMemberAttribute =
                prop
                    .GetCustomAttributes()
                    .OfType<System.Runtime.Serialization.DataMemberAttribute>()
                    .Cast<System.Runtime.Serialization.DataMemberAttribute>()
                    .FirstOrDefault();
            string fieldName = dataMemberAttribute?.Name?? prop.Name;
            object? correspondingItem = dictionary.ContainsKey(fieldName)? dictionary[fieldName] : null;
            if (correspondingItem == null && dictionary.ContainsKey(prop.Name))
                correspondingItem = dictionary[prop.Name];
            
            if (itemProperty != null)
                Assert.Equal(correspondingItem, itemProperty);
            else
                Assert.Null(correspondingItem);
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
