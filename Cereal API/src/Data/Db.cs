using Dapper;
using System.Data;

namespace Upoke.Api.Data;

public class Db
{
    private readonly SqlConnectionFactory _factory;
    public Db(SqlConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<T>(sql, param);
    }
    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<T>(sql, param);
    }
    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteAsync(sql, param);
    }
}