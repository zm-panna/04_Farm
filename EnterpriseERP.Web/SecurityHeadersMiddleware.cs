namespace EnterpriseERP.Web;

/// <summary>Browser defenses with self-hosted scripts only. Inline styles remain enabled for the existing MVC UI.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
 public async Task Invoke(HttpContext context)
 {
  context.Response.OnStarting(() => { Apply(context); return Task.CompletedTask; });
  Apply(context);
  await next(context);
  if (!context.Response.HasStarted) Apply(context);
 }
 private static void Apply(HttpContext context)
 {
  var headers = context.Response.Headers;
  headers["X-Content-Type-Options"] = "nosniff";
  headers["Referrer-Policy"] = "no-referrer";
  headers["X-Frame-Options"] = "DENY";
  var scanner = string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/scan", StringComparison.OrdinalIgnoreCase);
  headers["Permissions-Policy"] = scanner ? "camera=(self), microphone=(), geolocation=()" : "camera=(), microphone=(), geolocation=()";
  headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'self'; frame-ancestors 'none'; object-src 'none'; form-action 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self'";
  if (scanner) headers["Content-Security-Policy"] = headers["Content-Security-Policy"].ToString().Replace("script-src 'self'", "script-src 'self' 'wasm-unsafe-eval'", StringComparison.Ordinal);
  if (context.User.Identity?.IsAuthenticated == true || context.Request.Path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase))
  {
   headers.CacheControl = "no-store, no-cache, max-age=0, private";
   headers.Pragma = "no-cache";
   headers.Expires = "0";
  }
 }
}
