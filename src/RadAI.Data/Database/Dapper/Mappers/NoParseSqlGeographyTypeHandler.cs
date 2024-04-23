using Dapper;
using Microsoft.SqlServer.Types;
using System.Data;

namespace RadAI.Database.Dapper;

public class NoParseSqlGeographyTypeHandler : SqlMapper.TypeHandler<SqlGeography>
{
    public override Microsoft.SqlServer.Types.SqlGeography Parse(object value)
    { return default; }

    public override void SetValue(IDbDataParameter parameter, Microsoft.SqlServer.Types.SqlGeography value)
    { /* NO-OP */ }
}
