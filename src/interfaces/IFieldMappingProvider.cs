using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;

namespace src;

public interface IFieldMappingProvider
{
    IEnumerable<FieldMapping> GetFieldMappings(SqlSelectClause selectClause, Type inputType);
}
