using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Infrastructure;

/// <summary>
/// Represents plugin route provider
/// </summary>
public class RouteProvider : BaseRouteProvider, IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var lang = GetLanguageRoutePattern();

        endpointRouteBuilder.MapControllerRoute(name: Defaults.Route.Configuration,
            pattern: "Admin/PaymentPayPalExpressCheckout/Configure",
            defaults: new { controller = "PaymentPayPalExpressCheckout", action = "Configure", area = AreaNames.ADMIN });

        //Submit PayPal Express Checkout button
        endpointRouteBuilder.MapControllerRoute(name: Defaults.Route.Submit,
            pattern: $"{lang}/Plugins/PaymentPayPalExpressCheckout/SubmitButton",
            defaults: new { controller = "PaymentPayPalExpressCheckout", action = "SubmitButton" });

        // return handler
        endpointRouteBuilder.MapControllerRoute(name: Defaults.Route.Return,
            pattern: $"{lang}/Plugins/PaymentPayPalExpressCheckout/ReturnHandler",
            defaults: new { controller = "PaymentPayPalExpressCheckout", action = "Return" });

        // set shipping method
        endpointRouteBuilder.MapControllerRoute(name: Defaults.Route.ShippingMethod,
            pattern: $"{lang}/Plugins/PaymentPayPalExpressCheckout/SetShippingMethod",
            defaults: new { controller = "PaymentPayPalExpressCheckout", action = "SetShippingMethod" });

        // Confirm order
        endpointRouteBuilder.MapControllerRoute(name: Defaults.Route.Confirm,
            pattern: $"{lang}/Plugins/PaymentPayPalExpressCheckout/Confirm",
            defaults: new { controller = "PaymentPayPalExpressCheckout", action = "Confirm" });

        //IPN
        endpointRouteBuilder.MapControllerRoute(name: Defaults.Route.Ipn,
            pattern: "Plugins/PaymentPayPalExpressCheckout/IPNHandler",
            defaults: new { controller = "PaymentPayPalExpressCheckoutIpn", action = "IPNHandler" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}