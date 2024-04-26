﻿using SqlKata.Compilers;
using static RadAI.Data.Database.Enums;

namespace RadAI.Data.Database;

public class FirebirdQueryBuildingService : BaseQueryBuildingService
{
    private readonly FirebirdCompiler _compiler = new FirebirdCompiler();

    public string BuildSqlQueryString(
        Statement statement,
        Case casing = Case.AsIs,
        bool sqlWithParameters = true)
    {
        if (sqlWithParameters)
        {
            return _compiler
                .Compile(BuildQueryFromStatement(statement, casing))
                .ToString();
        }
        else
        {
            return _compiler
                .Compile(BuildQueryFromStatement(statement, casing))
                .Sql;
        }
    }
}