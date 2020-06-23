namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    /// <summary>
    /// Represents plugin constants
    /// </summary>
    public class Defaults
    {
        /// <summary>
        /// Gets PayPal button logo URL
        /// </summary>
        public static string CheckoutButtonImageUrl => "https://www.paypalobjects.com/webstatic/en_US/i/buttons/checkout-logo-medium.png";

        /// <summary>
        /// Gets the key for a payment info holder entry
        /// </summary>
        public static string ProcessPaymentRequestKey => "OrderPaymentInfo";

        /// <summary>
        ///  Gets the key for CheckoutPaymentResponseType entry
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
    }
}