// Cereal API/src/Data/UsersRepository.cs
using CerealAPI.Models;
using Dapper;

namespace CerealAPI.Data.Repository;

public sealed class UsersRepository
{
    private readonly SqlConnectionCeral _factory;

    public UsersRepository(SqlConnectionCeral factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        const string sql = @"SELECT Id, Username, PasswordHash, Role, CreatedAt
                             FROM dbo.Users
                             WHERE Id = @id";
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { id });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = @"SELECT TOP 1 Id, Username, PasswordHash, Role, CreatedAt
                             FROM dbo.Users
                             WHERE Username = @username";
        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { username });
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        const string sql = @"SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Users WHERE Username=@username) THEN 1 ELSE 0 END";
        using var conn = _factory.Create();
        var exists = await conn.ExecuteScalarAsync<int>(sql, new { username });
        return exists == 1;
    }

    public async Task<int> CreateAsync(string username, string passwordHash, string role = "user")
    {
        const string sql = @"
INSERT INTO dbo.Users (Username, PasswordHash, Role)
VALUES (@username, @passwordHash, @role);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(sql, new { username, passwordHash, role });
    }

    public async Task<int> UpdatePasswordHashAsync(int id, string newHash)
    {
        const string sql = @"UPDATE dbo.Users SET PasswordHash=@newHash WHERE Id=@id";
        using var conn = _factory.Create();
        return await conn.ExecuteAsync(sql, new { id, newHash });
    }

    public async Task<int> UpdateRoleAsync(int id, string role)
    {
        const string sql = @"UPDATE dbo.Users SET Role=@role WHERE Id=@id";
        using var conn = _factory.Create();
        return await conn.ExecuteAsync(sql, new { id, role });
    }
}
