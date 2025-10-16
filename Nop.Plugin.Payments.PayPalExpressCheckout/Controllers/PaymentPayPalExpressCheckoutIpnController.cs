using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Controllers;

public class PaymentPayPalExpressCheckoutIpnController : Controller
{
    #region Fields

    private readonly PayPalIPNService _payPalIPNService;

    #endregion

    #region Ctor

    public PaymentPayPalExpressCheckoutIpnController(PayPalIPNService payPalIPNService)
    {
        _payPalIPNService = payPalIPNService;
    }

    #endregion

    #region Methods

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> IPNHandler()
    {
        using var reader = new StreamReader(Request.Body, Encoding.ASCII);

        var ipnData = await reader.ReadToEndAsync();
        await _payPalIPNService.HandleIPNAsync(ipnData);

        return Ok();
    }

    #endregion
}