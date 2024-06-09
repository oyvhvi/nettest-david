using Dapper;
using System.Data.Common;

namespace atn062024.Tests.Helpers;

public static class SchemaHelper
{
    public static async Task CreateSchemaAsync(DbDataSource dataSource, CancellationToken ct)
    {
        await using var connection = dataSource.CreateConnection();
        await connection.OpenAsync(ct);

        var sql = await GetSqlScript("create_schema.sql", ct);

        await connection.ExecuteAsync(sql);
    }

    private static async Task<string> GetSqlScript(string filename, CancellationToken ct)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sql", filename);

        if (!File.Exists(path))
            throw new InvalidOperationException($"could not find file {path}");

        return await File.ReadAllTextAsync(path, ct);
    }
}
