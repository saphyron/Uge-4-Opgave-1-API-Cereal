using Microsoft.Data.SqlClient;
using System.Data;

namespace Upoke.Api.Data;

public class SqlConnectionFactory
{
    private readonly string _connString;
    public SqlConnectionFactory(string connString) => _connString = connString;

    public IDbConnection Create() => new SqlConnection(_connString);
}