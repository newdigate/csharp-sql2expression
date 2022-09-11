using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Reflection;

namespace src;

public class FieldMappingProvider : IFieldMappingProvider
{
    private readonly ITypeMapper _typeMapper;
    private readonly IUniqueNameProviderFactory _uniqueNameProviderFactory;

    public FieldMappingProvider(ITypeMapper typeMapper, IUniqueNameProviderFactory uniqueNameProviderFactory)
    {
        _typeMapper = typeMapper;
        _uniqueNameProviderFactory = uniqueNameProviderFactory;
    }

    public IEnumerable<FieldMapping> GetFieldMappings(SqlSelectClause selectClause, Type inputType)
    {
        List<FieldMapping> result = new List<FieldMapping>();

        foreach (SqlSelectExpression sqlSelectExpression in selectClause.SelectExpressions)
        {
            switch (sqlSelectExpression)
            {
                case SqlSelectScalarExpression sqlSelectScalarExpression:
                {
                    switch (sqlSelectScalarExpression.Expression)
                    {
                        case SqlScalarRefExpression sqlSelectScalarRefExpression:
                            {
                                SqlMultipartIdentifier m = sqlSelectScalarRefExpression.MultipartIdentifier;
                                PropertyInfo? propInfo = null;
                                string? propertyName = null;
                                List<string>? inputFieldNames = new List<string>();
                                switch (m.Count)
                                {
                                    case 1:
                                        {
                                            propertyName = $"{m.First().Sql}";
                                            inputFieldNames.Add(propertyName);
                                            propInfo = inputType.GetProperty(propertyName);
                                            break;
                                        }

                                    case 2:
                                        {
                                            propertyName = $"{m.Last().Sql}";
                                            inputFieldNames.Add(m.First().Sql);
                                            inputFieldNames.Add(propertyName);
                                            propInfo = inputType.GetProperty(propertyName);
                                            break;
                                        }
                                    case 3:
                                        {
                                            string typeName = $"{m.First().Sql}.{m.Skip(1).First().Sql}";
                                            string colName = m.Last().Sql;
                                            inputFieldNames.Add(typeName);
                                            inputFieldNames.Add(colName);
                                            propertyName = colName;
                                            Type? mappedType = _typeMapper.GetMappedType(typeName);
                                            if (mappedType == null)
                                                break;

                                            propInfo = mappedType.GetProperty(colName);
                                            break;
                                        }
                                }
                                if (propInfo != null)
                                {
                                    FieldMapping f = new FieldMapping()
                                    {
                                        InputFieldName = inputFieldNames,
                                        OutputFieldName = sqlSelectScalarExpression.Alias?.Sql ?? m?.ToString().Replace(".", "_"),
                                        FieldType = propInfo.PropertyType,
                                        IsNullable = (propInfo.GetCustomAttribute<NullableAttribute>() != null)
                                    };
                                    result.Add(f);
                                }
                                break;
                            }
                    }
                    break;
                }

                case SqlSelectStarExpression sqlSelectStarExpression:
                {
                    DynamicDataSetElementAttribute? dynamicDataSetElementAttribute =
                        inputType
                            .GetCustomAttributes()
                            .OfType<DynamicDataSetElementAttribute>()
                            .Cast<DynamicDataSetElementAttribute>()
                            .FirstOrDefault();
                    bool classIsComposite = dynamicDataSetElementAttribute != null;
                    if (classIsComposite)
                    {
                        result.AddRange( GetCompositeFieldMappings(inputType));
                    }
                    else
                    {
                        PropertyInfo[] properties = inputType.GetProperties();
                        foreach (PropertyInfo property in properties)
                        {
                            System.Runtime.Serialization.DataMemberAttribute? dataMemberAttribute =
                                property
                                    .GetCustomAttributes()
                                    .OfType<System.Runtime.Serialization.DataMemberAttribute>()
                                    .Cast<System.Runtime.Serialization.DataMemberAttribute>()
                                    .FirstOrDefault();

                            string? aliasName = dataMemberAttribute?.Name ?? property.Name;

                            FieldMapping f = new FieldMapping()
                            {
                                InputFieldName = new List<string>() { property.Name }, // inputFieldNames,
                                OutputFieldName = aliasName?.Replace(".", "_"),
                                FieldType = property.PropertyType,
                                IsNullable = (property.GetCustomAttribute<NullableAttribute>() != null)
                                //Alias = aliasName
                            };
                            result.Add(f);
                        }
                    }

                    break;
                }
            }
        }

        return result;
    }

    private IEnumerable<FieldMapping> GetCompositeFieldMappings(Type inputType)
    {
        List<FieldMapping> result = new List<FieldMapping>();
        PropertyInfo[] properties = inputType.GetProperties();
        IUniqueNameProvider uniqueNameProvider = _uniqueNameProviderFactory.Create();
        foreach (PropertyInfo compositeProperty in properties)
        {
            IEnumerable<FieldMapping> mappings = 
                GetFieldMappingsFromProperties(
                    new string[] {compositeProperty.Name}, 
                    compositeProperty.PropertyType, 
                    uniqueNameProvider,
                    compositeProperty.GetCustomAttribute<NullableAttribute>() != null);
            result.AddRange(mappings);
        }
        return result;
    }


    public IEnumerable<FieldMapping> GetFieldMappingsFromProperties(IEnumerable<string> navigation,  Type propertyType, IUniqueNameProvider uniqueNameProvider, bool isNullable)
    {
        List<FieldMapping> result = new List<FieldMapping>();
        foreach (PropertyInfo property in propertyType.GetProperties())
        {
            System.Runtime.Serialization.DataMemberAttribute? dataMemberAttribute =
                property
                    .GetCustomAttributes()
                    .OfType<System.Runtime.Serialization.DataMemberAttribute>()
                    .Cast<System.Runtime.Serialization.DataMemberAttribute>()
                    .FirstOrDefault();

            string? aliasName = dataMemberAttribute?.Name ?? property.Name;
            FieldMapping f = new FieldMapping()
            {
                InputFieldName = navigation.Union(new string[] { property.Name }).ToList(), // inputFieldNames,
                OutputFieldName = uniqueNameProvider.GetUniqueName(aliasName),
                FieldType = property.PropertyType,
                IsNullable = isNullable
            };
            result.Add(f);
        }
        return result;
    }
}

public interface ICompositeFieldProvider {
    IEnumerable<Field> GetCompositeFields(Type inputType);
}

