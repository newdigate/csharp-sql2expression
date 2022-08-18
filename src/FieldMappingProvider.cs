using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Reflection;

namespace src;

public class FieldMappingProvider {
    private readonly TypeMapper _typeMapper;
    private readonly IUniqueNameProviderFactory _uniqueNameProviderFactory;

    public FieldMappingProvider(TypeMapper typeMapper, IUniqueNameProviderFactory uniqueNameProviderFactory)
    {
        _typeMapper = typeMapper;
        _uniqueNameProviderFactory = uniqueNameProviderFactory;
    }

    public IEnumerable<FieldMapping> GetFieldMappings(SqlSelectClause selectClause, Type inputType) {
        
        List<FieldMapping> result = new List<FieldMapping>();
        foreach (SqlSelectExpression sqlSelectExpression in selectClause.SelectExpressions) {

            switch (sqlSelectExpression) {
                case SqlSelectScalarExpression sqlSelectScalarExpression :
                {
                    switch(sqlSelectScalarExpression.Expression) {
                        case SqlScalarRefExpression sqlSelectScalarRefExpression: {
                            SqlMultipartIdentifier m = sqlSelectScalarRefExpression.MultipartIdentifier;
                            PropertyInfo? propInfo = null;
                            string? propertyName = null;
                            List<string>? inputFieldNames = new List<string>();
                            switch (m.Count) {
                                case 1: {
                                    propertyName = $"{m.First().Sql}";
                                    inputFieldNames.Add(propertyName);
                                    propInfo = inputType.GetProperty(propertyName);
                                    break;
                                }
                                case 3: {
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
                            if (propInfo != null) {
                                FieldMapping f = new FieldMapping() 
                                {
                                    InputFieldName = inputFieldNames,
                                    OutputFieldName = m.ToString().Replace(".","_"),
                                    FieldType = propInfo.PropertyType
                                };
                                result.Add(f);
                            }
                            break;
                        }
                    }

                    break;
                }

                case SqlSelectStarExpression sqlSelectStarExpression: {

                    PropertyInfo[] properties = inputType.GetProperties();

                    DynamicDataSetElementAttribute? dynamicDataSetElementAttribute = 
                        inputType
                            .GetCustomAttributes()
                            .OfType<DynamicDataSetElementAttribute>()
                            .Cast<DynamicDataSetElementAttribute>()
                            .FirstOrDefault();
                    bool classIsComposite = dynamicDataSetElementAttribute != null;
                    if (classIsComposite) {
                        IUniqueNameProvider uniqueNameProvider = _uniqueNameProviderFactory.Create();
                        foreach (PropertyInfo compositeProperty in properties) {
                            foreach (PropertyInfo property in compositeProperty.PropertyType.GetProperties()) {
                                System.Runtime.Serialization.DataMemberAttribute? dataMemberAttribute =
                                    property
                                        .GetCustomAttributes()
                                        .OfType<System.Runtime.Serialization.DataMemberAttribute>()
                                        .Cast<System.Runtime.Serialization.DataMemberAttribute>()
                                        .FirstOrDefault();
                                        
                                string? aliasName =  dataMemberAttribute?.Name ?? property.Name;
                                FieldMapping f = new FieldMapping() 
                                {
                                    InputFieldName = new List<string>() {compositeProperty.Name, property.Name}, // inputFieldNames,
                                    OutputFieldName = uniqueNameProvider.GetUniqueName(aliasName),
                                    FieldType = property.PropertyType,
                                    //Alias = aliasName
                                };
                                result.Add(f);
                            }
                        }
                    } else {
                        foreach (PropertyInfo property in properties) {
                            System.Runtime.Serialization.DataMemberAttribute? dataMemberAttribute =
                                property
                                    .GetCustomAttributes()
                                    .OfType<System.Runtime.Serialization.DataMemberAttribute>()
                                    .Cast<System.Runtime.Serialization.DataMemberAttribute>()
                                    .FirstOrDefault();
                                    
                            string? aliasName =  dataMemberAttribute?.Name ?? property.Name;
                            
                            FieldMapping f = new FieldMapping() 
                            {
                                InputFieldName = new List<string>() {property.Name}, // inputFieldNames,
                                OutputFieldName = aliasName?.Replace(".","_"),
                                FieldType = property.PropertyType,
                                //Alias = aliasName
                            };
                            result.Add(f);
                        }
                    }

                    break;
                }

            }


        }

        //result.AddRange(GetFields(sqlJoinStatement.Left));
        return result;
    }

}
