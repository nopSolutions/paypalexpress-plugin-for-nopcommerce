using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Infrastructure
{
    /// <summary>
    /// Represents plugin route provider
    /// </summary>
    public class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="endpointRouteBuilder">Route builder</param>
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            //Submit PayPal Express Checkout button
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayPalExpressCheckout.SubmitButton",
                "Plugins/PaymentPayPalExpressCheckout/SubmitButton",
                new { controller = "PaymentPayPalExpressCheckout", action = "SubmitButton" });

            // return handler
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayPalExpressCheckout.ReturnHandler",
                "Plugins/PaymentPayPalExpressCheckout/ReturnHandler",
                new { controller = "PaymentPayPalExpressCheckout", action = "Return" });

            // set shipping method
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayPalExpressCheckout.SetShippingMethod",
                "Plugins/PaymentPayPalExpressCheckout/SetShippingMethod",
                new { controller = "PaymentPayPalExpressCheckout", action = "SetShippingMethod" });

            // Confirm order
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayPalExpressCheckout.Confirm",
                "Plugins/PaymentPayPalExpressCheckout/Confirm",
                new { controller = "PaymentPayPalExpressCheckout", action = "Confirm" });

            //IPN
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PayPalExpressCheckout.IPNHandler",
                "Plugins/PaymentPayPalExpressCheckout/IPNHandler",
                new { controller = "PaymentPayPalExpressCheckout", action = "IPNHandler" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}