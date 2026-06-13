using Microsoft.AspNetCore.Mvc;
using Sallimni.Infrastructure.Services;

namespace Sallimni.Api.Controllers;

/// <summary>حذف الحساب داخل التطبيق (إلزامي للمتجرين — قسم 15).</summary>
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly AccountService _account;
    public AccountController(AccountService account) => _account = account;

    [HttpDelete("customer/{id:guid}")]
    public async Task<IActionResult> DeleteCustomer(Guid id, CancellationToken ct)
    {
        try { await _account.DeleteCustomerAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("merchant/{id:guid}")]
    public async Task<IActionResult> DeleteMerchant(Guid id, CancellationToken ct)
    {
        try { await _account.DeleteMerchantAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("driver/{id:guid}")]
    public async Task<IActionResult> DeleteDriver(Guid id, CancellationToken ct)
    {
        try { await _account.DeleteDriverAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
