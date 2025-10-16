using Nop.Core;

namespace Nop.Plugin.Payments.PayPalExpressCheckout;

/// <summary>
/// Represents plugin constants
/// </summary>
public class Defaults
{
    /// <summary>
    /// Gets the plugin system name
    /// </summary>
    public static string SystemName => "Payments.PayPalExpressCheckout";

    /// <summary>
    /// Gets the user agent used to request third-party services
    /// </summary>
    public static string UserAgent => $"nopCommerce-{NopVersion.FULL_VERSION}";

    /// <summary>
    /// Gets the service URL
    /// </summary>
    public static (string Sandbox, string Live) ServiceUrl => ("https://www.sandbox.paypal.com/us/cgi-bin/webscr", "https://www.paypal.com/us/cgi-bin/webscr");

    /// <summary>
    /// Gets a default period (in seconds) before the request times out
    /// </summary>
    public static int RequestTimeout => 20;

    /// <summary>
    /// Gets PayPal button logo URL
    /// </summary>
    public static string CheckoutButtonImageUrl => "https://www.paypalobjects.com/webstatic/en_US/i/buttons/checkout-logo-medium.png";

    /// <summary>
    /// Gets nopCommerce partner code
    /// </summary>
    public static string PartnerCode => "nopCommerce_SP";

    /// <summary>
    /// Gets the key for CheckoutPaymentResponseType entry
    /// </summary>
    public static string CheckoutPaymentResponseTypeKey => "express-checkout-response-type";

    /// <summary>
    /// Gets the key for Express Checkout token
    /// </summary>
    public static string PaypalTokenKey => "PaypalToken";

    /// <summary>
    /// Gets the key for an external unique identifier of a particular PayPal account
    /// </summary>
    public static string PaypalPayerIdKey => "PaypalPayerId";

    /// <summary>
    /// Gets the key for an error occurred during the checkout process
    /// </summary>
    public static string CheckoutErrorMessageKey => "paypal-ec-error";

    #region Route names

    /// <summary>
    /// Represents the route names
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Gets the configuration route name
        /// </summary>
        public static string Configuration => "Plugin.Payments.PayPalExpressCheckout.Configure";

        /// <summary>
        /// Gets the submit route name
        /// </summary>
        public static string Submit => "Plugin.Payments.PayPalExpressCheckout.SubmitButton";

        /// <summary>
        /// Gets the return handler route name
        /// </summary>
        public static string Return => "Plugin.Payments.PayPalExpressCheckout.ReturnHandler";

        /// <summary>
        /// Gets the shipping method route name
        /// </summary>
        public static string ShippingMethod => "Plugin.Payments.PayPalExpressCheckout.SetShippingMethod";

        /// <summary>
        /// Gets the confirm route name
        /// </summary>
        public static string Confirm => "Plugin.Payments.PayPalExpressCheckout.Confirm";

        /// <summary>
        /// Gets the IPN handler route name
        /// </summary>
        public static string Ipn => "Plugin.Payments.PayPalExpressCheckout.IPNHandler";
    }

    #endregion
}