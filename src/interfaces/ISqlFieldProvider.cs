using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace src;

public interface ISqlFieldProvider
{
    IEnumerable<Field> GetFields(SqlJoinTableExpression sqlJoinStatement);
    IEnumerable<Field> GetFields(SqlTableExpression sqlTableExpression);
}
