using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Submit PayPal Express Checkout button
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.SubmitButton",
                 "Plugins/PaymentPayPalExpressCheckout/SubmitButton",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SubmitButton" });

            // return handler
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.ReturnHandler",
                 "Plugins/PaymentPayPalExpressCheckout/ReturnHandler",
                 new { controller = "PaymentPayPalExpressCheckout", action = "Return" });

            // set existing address
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.SetExistingAddress",
                 "Plugins/PaymentPayPalExpressCheckout/SetExistingAddress",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SetExistingAddress" });

            // set new shipping address
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.SetShippingAddress",
                 "Plugins/PaymentPayPalExpressCheckout/SetShippingAddress",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SetShippingAddress" });

            // set shipping method
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.SetShippingMethod",
                 "Plugins/PaymentPayPalExpressCheckout/SetShippingMethod",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SetShippingMethod" });

            // Confirm order
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.Confirm",
                 "Plugins/PaymentPayPalExpressCheckout/Confirm",
                 new { controller = "PaymentPayPalExpressCheckout", action = "Confirm" });

            //IPN
            routeBuilder.MapRoute("Plugin.Payments.PayPalExpressCheckout.IPNHandler",
                 "Plugins/PaymentPayPalExpressCheckout/IPNHandler",
                 new { controller = "PaymentPayPalExpressCheckout", action = "IPNHandler" });
        }

        public int Priority => 0;
    }
}