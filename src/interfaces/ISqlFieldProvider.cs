using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace src;

public interface ISqlFieldProvider
{
    IEnumerable<Field> GetOuterFields(SqlJoinTableExpression sqlJoinStatement);
    IEnumerable<Field> GetFields(SqlTableExpression sqlTableExpression);
}
