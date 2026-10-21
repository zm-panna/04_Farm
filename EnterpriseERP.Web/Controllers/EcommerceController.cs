using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class EcommerceController(EcommerceService ecommerce) : Controller
{
    [HttpGet("/Ecommerce")][HttpGet("/Ecommerce/Home")]
    public async Task<IActionResult> Index(CancellationToken ct, int page = 1, string? search = null, string? category = null)
    { try { return View(await ecommerce.Browse(page, search, category, ct)); } catch (ArgumentException) { return BadRequest("Invalid product search or category."); } }
    [HttpGet("/Ecommerce/manage")]
    public async Task<IActionResult> Manage(CancellationToken ct) => View(await ecommerce.Manage(ct));
    [HttpPost("/Ecommerce/customer-link")]
    public Task<IActionResult> LinkCustomer(string existingUserId, int partyId, bool confirmIdentityVerified, CancellationToken ct)
        => Mutate(async () => { await ecommerce.LinkCustomer(existingUserId, partyId, confirmIdentityVerified, ct); }, "/Ecommerce/manage", "Verified customer identity linked.");
    [HttpPost("/Ecommerce/product")]
    public Task<IActionResult> Product(long? id, int stockItemId, string title, string? description, string? category, decimal netUnitPrice, long taxPolicyId, bool published, string? revisionToken, CancellationToken ct)
        => Mutate(async () => { await ecommerce.PublishProduct(id, stockItemId, title, description, category, netUnitPrice, taxPolicyId, published, revisionToken, ct); }, "/Ecommerce/manage", "Catalog product saved with approved currency and tax policy.");
    [HttpGet("/Ecommerce/cart")]
    public async Task<IActionResult> Cart(CancellationToken ct)
    {
        try { return View(await ecommerce.Cart(ct)); }
        catch (InvalidOperationException ex) { ViewData["Notice"] = ex.Message; return View("Unavailable"); }
    }
    [HttpPost("/Ecommerce/cart/line")]
    public Task<IActionResult> CartLine(long productId, decimal quantity, string? expectedCartToken, CancellationToken ct)
        => Mutate(() => ecommerce.SetCartLine(productId, quantity, expectedCartToken, ct), "/Ecommerce/cart", "Cart updated using server prices and approved tax.");
    [HttpPost("/Ecommerce/checkout")]
    public async Task<IActionResult> Checkout(string checkoutToken, bool confirmOrder, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Enter valid form values."; return Redirect("/Ecommerce/cart"); }
        try { var order = await ecommerce.Checkout(checkoutToken, confirmOrder, ct); TempData["Success"] = "Order received and stock reserved for staff review. Payment remains unverified; nothing was charged."; return Redirect($"/Ecommerce/orders/{order.Id}"); }
        catch (Exception ex) when (SafeFailure(ex)) { TempData["Error"] = Error(ex); return Redirect("/Ecommerce/cart"); }
    }
    [HttpGet("/Ecommerce/orders")]
    public async Task<IActionResult> Orders(CancellationToken ct, int page = 1, string? search = null, string? status = null)
    {
        try { return View(await ecommerce.Orders(false, page, search, status, ct)); }
        catch (InvalidOperationException ex) { ViewData["Notice"] = ex.Message; return View("Unavailable"); }
        catch (ArgumentException) { return BadRequest("Invalid order filters."); }
    }
    [HttpGet("/Ecommerce/staff/orders")]
    public async Task<IActionResult> StaffOrders(CancellationToken ct, int page = 1, string? search = null, string? status = null)
    { try { return View("Orders", await ecommerce.Orders(true, page, search, status, ct)); } catch (ArgumentException) { return BadRequest("Invalid order filters."); } }
    [HttpGet("/Ecommerce/orders/{id:long}")]
    public Task<IActionResult> Order(long id, CancellationToken ct) => Detail(id, false, ct);
    [HttpGet("/Ecommerce/staff/orders/{id:long}")]
    public Task<IActionResult> StaffOrder(long id, CancellationToken ct) => Detail(id, true, ct);
    private async Task<IActionResult> Detail(long id, bool staff, CancellationToken ct)
    {
        try { return View("Order", await ecommerce.Order(id, staff, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) when (!staff) { ViewData["Notice"] = ex.Message; return View("Unavailable"); }
    }
    [HttpPost("/Ecommerce/staff/orders/{id:long}/approve")]
    public Task<IActionResult> Approve(long id, string stateToken, string reviewNote, bool confirmReview, CancellationToken ct)
        => Mutate(() => ecommerce.Approve(id, stateToken, reviewNote, confirmReview, ct), $"/Ecommerce/staff/orders/{id}", "Order reviewed and approved for fulfillment.");
    [HttpPost("/Ecommerce/orders/{id:long}/cancel")]
    public Task<IActionResult> Cancel(long id, string stateToken, string reason, CancellationToken ct)
        => Mutate(() => ecommerce.Cancel(id, stateToken, reason, false, ct), $"/Ecommerce/orders/{id}", "Order cancelled and reserved stock released.");
    [HttpPost("/Ecommerce/staff/orders/{id:long}/cancel")]
    public Task<IActionResult> StaffCancel(long id, string stateToken, string reason, CancellationToken ct)
        => Mutate(() => ecommerce.Cancel(id, stateToken, reason, true, ct), $"/Ecommerce/staff/orders/{id}", "Order cancelled and reserved stock released.");
    [HttpPost("/Ecommerce/staff/orders/{id:long}/fulfill")]
    public Task<IActionResult> Fulfill(long id, string stateToken, bool confirmFulfillment, CancellationToken ct)
        => Mutate(() => ecommerce.Fulfill(id, stateToken, confirmFulfillment, ct), $"/Ecommerce/staff/orders/{id}", "Stock, sales, configured tax and customer receivable posted. Payment remains unverified.");
    [HttpPost("/Ecommerce/staff/orders/{id:long}/return")]
    public Task<IActionResult> Return(long id, long lineId, decimal quantity, string requestToken, string reason, bool confirmReceived, CancellationToken ct)
        => Mutate(async () => { await ecommerce.Return(id, lineId, quantity, requestToken, reason, confirmReceived, ct); }, $"/Ecommerce/staff/orders/{id}", "Return received; stock and original sales/tax allocation reversed. Any cash refund needs separate finance processing.");
    private async Task<IActionResult> Mutate(Func<Task> action, string redirect, string success)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Enter valid form values."; return Redirect(redirect); }
        try { await action(); TempData["Success"] = success; }
        catch (Exception ex) when (SafeFailure(ex)) { TempData["Error"] = Error(ex); }
        return Redirect(redirect);
    }
    private static bool SafeFailure(Exception ex) => ex is ArgumentException or InvalidOperationException or DbUpdateException or KeyNotFoundException;
    private static string Error(Exception ex) => ex is DbUpdateException ? "This record changed or conflicts with existing data. Reload and check the current order before retrying." : ex.Message;
}
