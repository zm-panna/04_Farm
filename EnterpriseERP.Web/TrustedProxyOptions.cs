using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
namespace EnterpriseERP.Web;
/// <summary>Dedicated gateway profile. Forwarded headers are not trusted by default.</summary>
public sealed record TrustedProxyOptions(IPAddress Address, Uri PublicOrigin)
{
    public static TrustedProxyOptions? Read(IConfiguration configuration, bool development)
    {
        if (!configuration.GetValue<bool>("TrustedProxy:Enabled")) return null;
        if (!IPAddress.TryParse(configuration["TrustedProxy:KnownProxyIp"], out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            throw new InvalidOperationException("TrustedProxy requires one explicit gateway IP address; wildcard networks are prohibited.");
        if (!Uri.TryCreate(configuration["TrustedProxy:PublicOrigin"], UriKind.Absolute, out var origin) || origin.UserInfo != "" || origin.Query != "" || origin.Fragment != "" || origin.AbsolutePath != "/"
            || (origin.Scheme != "https" && !(development && origin.Scheme == "http" && origin.IsLoopback)))
            throw new InvalidOperationException("TrustedProxy requires an exact HTTPS public origin. Development HTTP is loopback only.");
        var hosts = (configuration["AllowedHosts"] ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (hosts.Length != 1 || !hosts[0].Equals(origin.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The gateway profile requires AllowedHosts to equal the one public hostname, without scheme or port.");
        return new(address, origin);
    }
    public ForwardedHeadersOptions Headers()
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
        options.KnownProxies.Clear(); options.KnownIPNetworks.Clear(); options.AllowedHosts.Clear();
        options.KnownProxies.Add(Address); options.AllowedHosts.Add(PublicOrigin.Host); return options;
    }
    public bool Accepts(HttpContext context) => context.Connection.RemoteIpAddress is { } peer && peer.MapToIPv6().Equals(Address.MapToIPv6())
        && string.Equals(context.Request.Host.Value, PublicOrigin.Authority, StringComparison.OrdinalIgnoreCase)
        && context.Request.Headers["X-Forwarded-Host"].ToString().Equals(PublicOrigin.Authority, StringComparison.OrdinalIgnoreCase)
        && context.Request.Headers["X-Forwarded-Proto"].ToString().Equals(PublicOrigin.Scheme, StringComparison.Ordinal);
}
