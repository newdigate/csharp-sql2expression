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
                            
                            switch (m.Count) {
                                case 1: {
                                    string propertyName = $"{m.First().Sql}";
                                    PropertyInfo propInfo = inputType.GetProperty(propertyName);
                                    FieldMapping f = new FieldMapping() 
                                    {
                                        InputFieldName = new List<string>() {propertyName },
                                        OutputFieldName = m.ToString().Replace(".","_"),
                                        FieldType = propInfo.PropertyType
                                    };
                                    result.Add(f);
                                    break;
                                }
                                case 3: {
                                    string typeName = $"{m.First().Sql}.{m.Skip(1).First().Sql}";
                                    string colName = m.Last().Sql;
                                    
                                    Type? mappedType = _typeMapper.GetMappedType(typeName);
                                    if (mappedType == null)
                                        break;

                                    PropertyInfo propInfo = mappedType.GetProperty(colName);
                                    FieldMapping f = new FieldMapping() 
                                    {
                                        InputFieldName = new List<string>() {typeName, colName},
                                        OutputFieldName = m.ToString().Replace(".","_"),
                                        FieldType = propInfo.PropertyType
                                    };
                                    result.Add(f);
                                    break;
                                }
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
