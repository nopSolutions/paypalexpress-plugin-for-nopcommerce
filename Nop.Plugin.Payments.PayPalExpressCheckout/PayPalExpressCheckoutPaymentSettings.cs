using Nop.Core.Configuration;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    public class PayPalExpressCheckoutPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the API signature specified in PayPal account
        /// </summary>
        public string ApiSignature { get; set; }

        /// <summary>
        /// Gets or sets the API username specified in PayPal account
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the API password specified in PayPal account
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use live mode
        /// </summary>
        public bool IsLive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there is a business account
        /// </summary>
        public bool DoNotHaveBusinessAccount { get; set; }

        /// <summary>
        /// Gets or sets an email address
        /// </summary>
        public string EmailAddress { get; set; }

        /// <summary>
        /// Gets or sets a banner image URL
        /// </summary>
        public string LogoImageURL { get; set; }

        /// <summary>
        /// Gets or sets a color of the cart border on the PayPal page in a 6-character HTML hexadecimal ASCII color code format
        /// </summary>
        public string CartBorderColor { get; set; }

        /// <summary>
        /// Gets or sets a locale code of pages displayed by PayPal during Express Checkout
        /// </summary>
        public string LocaleCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to require confirmed shipping address
        /// </summary>
        public bool RequireConfirmedShippingAddress { get; set; }

        /// <summary>
        /// Gets or sets a payment Action
        /// </summary>
        public PaymentActionCodeType PaymentAction { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable debug logging
        /// </summary>
        public bool EnableDebugLogging { get; set; }
    }
}