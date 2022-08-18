using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Reflection;

namespace src;

public class FieldMappingProvider {
    private readonly TypeMapper _typeMapper;

    public FieldMappingProvider(TypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
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
            }


        }

        //result.AddRange(GetFields(sqlJoinStatement.Left));
        return result;
    }

}
