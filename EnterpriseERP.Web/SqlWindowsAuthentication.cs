using Microsoft.Data.SqlClient;

namespace EnterpriseERP.Web;

/// <summary>Validates an explicit integrated SQL profile without rewriting or logging secrets.</summary>
public static class SqlWindowsAuthentication
{
    public static void Validate(string? mode, string provider, string connectionString)
    {
        mode = mode?.Trim();
        if (string.IsNullOrEmpty(mode) || mode.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase))
            return;
        if (!mode.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database:Authentication must be ConnectionString or Windows.");
        if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Database:Authentication=Windows requires the SqlServer provider.");
        SqlConnectionStringBuilder sql;
        try { sql = new SqlConnectionStringBuilder(connectionString); }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        { throw new InvalidOperationException("Windows SQL authentication connection configuration is invalid."); }
        if (!sql.IntegratedSecurity || sql.ShouldSerialize("User ID") || sql.ShouldSerialize("Password")
            || sql.Authentication != SqlAuthenticationMethod.NotSpecified || sql.PersistSecurityInfo
            || string.IsNullOrWhiteSpace(sql.DataSource) || string.IsNullOrWhiteSpace(sql.InitialCatalog))
            throw new InvalidOperationException("Windows SQL authentication requires a server, database and Integrated Security=true, without SQL credentials or another authentication method.");
        var encrypt = sql.Encrypt.ToString();
        if (!(encrypt.Equals("Mandatory", StringComparison.OrdinalIgnoreCase)
            || encrypt.Equals("Strict", StringComparison.OrdinalIgnoreCase)
            || encrypt.Equals("True", StringComparison.OrdinalIgnoreCase)) || sql.TrustServerCertificate)
            throw new InvalidOperationException("Windows SQL authentication requires encryption and a verifiable server certificate.");
    }
}
