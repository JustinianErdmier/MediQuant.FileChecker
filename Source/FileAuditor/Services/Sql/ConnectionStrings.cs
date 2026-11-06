using Microsoft.Data.SqlClient;

namespace FileAuditor.Services.Sql;

/// <summary>
///     Builds connection strings for the audited database.
/// </summary>
public static class ConnectionStrings
{
    /// <summary>
    ///     Builds a Windows-authenticated connection string that trusts the server certificate.
    /// </summary>
    /// <param name="server">The server (data source) name.</param>
    /// <param name="database">The database (initial catalogue) name.</param>
    /// <returns>The connection string.</returns>
    public static string Build(string server, string database)
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = server,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true
        };

        return builder.ConnectionString;
    }
}