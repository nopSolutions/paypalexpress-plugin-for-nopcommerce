using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //Submit PayPal Express Checkout button
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.SubmitButton",
                 "Plugins/PaymentPayPalExpressCheckout/SubmitButton",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SubmitButton" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
                 );

            // return handler
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.ReturnHandler",
                 "Plugins/PaymentPayPalExpressCheckout/ReturnHandler",
                 new { controller = "PaymentPayPalExpressCheckout", action = "Return" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
                 );

            // set existing address
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.SetExistingAddress",
                 "Plugins/PaymentPayPalExpressCheckout/SetExistingAddress",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SetExistingAddress" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
                 );

            // set new shipping address
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.SetShippingAddress",
                 "Plugins/PaymentPayPalExpressCheckout/SetShippingAddress",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SetShippingAddress" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
                 );

            // set shipping method
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.SetShippingMethod",
                 "Plugins/PaymentPayPalExpressCheckout/SetShippingMethod",
                 new { controller = "PaymentPayPalExpressCheckout", action = "SetShippingMethod" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
                 );
            
            // Confirm order
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.Confirm",
                 "Plugins/PaymentPayPalExpressCheckout/Confirm",
                 new { controller = "PaymentPayPalExpressCheckout", action = "Confirm" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
                 );

            //IPN
            routes.MapRoute("Plugin.Payments.PayPalExpressCheckout.IPNHandler",
                 "Plugins/PaymentPayPalExpressCheckout/IPNHandler",
                 new { controller = "PaymentPayPalExpressCheckout", action = "IPNHandler" },
                 new[] { "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }
            );
        }

        public int Priority { get { return 0; } }
    }
}