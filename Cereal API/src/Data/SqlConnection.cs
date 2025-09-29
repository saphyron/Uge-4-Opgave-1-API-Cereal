using Microsoft.Data.SqlClient;
using System.Data;

namespace CerealAPI.Data;

public class SqlConnectionCeral
{
    private readonly string _connString;
    public SqlConnectionCeral(string connString) => _connString = connString;

    public IDbConnection Create() => new SqlConnection(_connString);
}