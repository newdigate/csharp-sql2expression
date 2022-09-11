using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace src;

public interface ISqlFieldProvider
{
    IEnumerable<Field> GetOuterFields(SqlJoinTableExpression sqlJoinStatement, bool isNullable=false);
    IEnumerable<Field> GetFields(SqlTableExpression sqlTableExpression, bool isNullable=false);
}
