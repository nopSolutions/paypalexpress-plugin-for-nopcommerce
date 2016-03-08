using Nop.Core.Configuration;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    public class PayPalExpressCheckoutPaymentSettings : ISettings
    {
        public string ApiSignature { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public bool IsLive { get; set; }

        public bool DoNotHaveBusinessAccount { get; set; }

        public string EmailAddress { get; set; }

        public string LogoImageURL { get; set; }
        public string CartBorderColor { get; set; }

        public string LocaleCode { get; set; }

        public bool RequireConfirmedShippingAddress { get; set; }

        public PaymentActionCodeType PaymentAction { get; set; }

        public decimal HandlingFee { get; set; }
        public bool HandlingFeeIsPercentage { get; set; }
        
        public bool EnableDebugLogging { get; set; }
    }
}