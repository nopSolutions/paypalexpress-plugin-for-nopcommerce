namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    /// <summary>
    /// Represents plugin constants
    /// </summary>
    public class Defaults
    {
        /// <summary>
        /// Gets the configuration view path
        /// </summary>
        public const string CONFIGURATION_VIEW_PATH = "~/Plugins/Payments.PayPalExpressCheckout/Views/Configure.cshtml";

        /// <summary>
        /// Gets the confirm view path
        /// </summary>
        public const string CONFIRM_VIEW_PATH = "~/Plugins/Payments.PayPalExpressCheckout/Views/Confirm.cshtml";

        /// <summary>
        /// PayPal button logo
        /// </summary>
        public const string CHECKOUT_BUTTON_IMAGE_URL = "https://www.paypalobjects.com/webstatic/en_US/i/buttons/checkout-logo-medium.png";
    }
}