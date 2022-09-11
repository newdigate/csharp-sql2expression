using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Reflection;

namespace src;

public class SqlFieldProvider : ISqlFieldProvider
{
    private readonly ITypeMapper _typeMapper;

    public SqlFieldProvider(ITypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }

    public IEnumerable<Field> GetOuterFields(SqlJoinTableExpression sqlJoinStatement, bool isNullable=false)
    {
        List<Field> result = new List<Field>();
        switch (sqlJoinStatement.JoinOperator) {
            case SqlJoinOperatorType.InnerJoin: {
                result.AddRange(GetFields(sqlJoinStatement.Left, isNullable));
                result.AddRange(GetFields(sqlJoinStatement.Right, isNullable)); 
                break;
            }
            case SqlJoinOperatorType.LeftOuterJoin: {
                result.AddRange(GetFields(sqlJoinStatement.Left, isNullable));
                result.AddRange(GetFields(sqlJoinStatement.Right, true)); 
                break;
            }
        }
        
        return result;
    }

    public IEnumerable<Field> GetFields(SqlTableExpression sqlTableExpression, bool isNullable=false)
    {
        List<Field> result = new List<Field>();
        switch (sqlTableExpression)
        {
            case SqlJoinTableExpression sqlJoinTableExpression:
            {
                IEnumerable<Field> fields = GetOuterFields(sqlJoinTableExpression);
                result.AddRange(fields);
                break;
            }

            case SqlTableRefExpression sqlTableRefExpression:
            {
                result.AddRange(GetFields(sqlTableRefExpression, isNullable));
                break;
            }
        }
        return result;
    }

    IEnumerable<Field> GetFields(SqlTableRefExpression sqlTableRefExpression, bool isNullable=false)
    {
        List<Field> result = new List<Field>();

        Type? mappedType = _typeMapper.GetMappedType(sqlTableRefExpression.Sql);
        if (mappedType == null)
            return result;

        Field f = new Field() { 
            FieldName = sqlTableRefExpression.Sql.ToString().Replace(".", "_"), 
            FieldType = mappedType,
            IsNullable = isNullable };
        result.Add(f);
        return result;
    }

    IEnumerable<Field> GetFields(SqlSelectClause selectClause, Type inputType)
    {
        List<Field> result = new List<Field>();
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

                                    switch (m.Count)
                                    {
                                        case 1: break;
                                        case 2: break;
                                        case 3:
                                            {
                                                string typeName = $"{m.First().Sql}.{m.Skip(1).First().Sql}";
                                                string colName = m.Last().Sql;

                                                Type mappedType = _typeMapper.GetMappedType(typeName);

                                                PropertyInfo propInfo = mappedType.GetProperty(colName);

                                                Field f = new Field()
                                                {
                                                    FieldName = m.ToString().Replace(".", "_"),
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