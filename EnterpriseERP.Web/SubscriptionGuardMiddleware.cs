namespace EnterpriseERP.Web;

/// <summary>Applies the same subscription policy to MVC and API business routes.</summary>
public sealed class SubscriptionGuardMiddleware(RequestDelegate next)
{
 private static bool AllowedDuringSuspension(HttpRequest request)
 {
  var path = request.Path;
  bool Under(string prefix) => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase);
  bool Exact(string expected) => string.Equals(path.Value?.TrimEnd('/'), expected, StringComparison.OrdinalIgnoreCase);
  var readOnly = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
  // Identity/context recovery and support remain available, but segment matching prevents prefix lookalikes.
  if (Under("/Account") || Under("/Context") || Under("/Support") || Under("/CustomerCare") || Under("/api/support")) return true;
  if (readOnly && (path == "/" || Exact("/Home/Error") || Under("/Subscription") || Under("/api/subscription")
    || Exact("/api/operations/subscription") || Under("/api/reports") || Under("/Reports") || Under("/ops")
    || Exact("/api/operations/summary") || Exact("/api/operations/summary.csv")
    || Exact("/api/advanced/farm-summary.pdf") || Exact("/api/advanced/trial-balance.xlsx")
    || Exact("/api/advanced/ledger") || Exact("/api/advanced/trial-balance") || Exact("/api/advanced/statement"))) return true;
  return HttpMethods.IsPost(request.Method) && (Exact("/api/operations/subscription/payment") || Exact("/Subscription/payment"));
 }

 public async Task Invoke(HttpContext http, SubscriptionService subscriptions)
 {
  if (http.User.Identity?.IsAuthenticated != true || AllowedDuringSuspension(http.Request))
  { await next(http); return; }
  try
  {
   var state = await subscriptions.State(DateOnly.FromDateTime(DateTime.UtcNow), http.RequestAborted);
   if (state.Status == "Suspended")
   {
    http.Response.StatusCode = StatusCodes.Status402PaymentRequired;
    await http.Response.WriteAsJsonAsync(new { error = "Subscription suspended", billing = "/api/operations/subscription" }, http.RequestAborted);
    return;
   }
  }
  catch (UnauthorizedAccessException)
  { http.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
  await next(http);
 }
}
