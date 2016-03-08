using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalSecurityService : IPayPalSecurityService
    {
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalSecurityService(PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public CustomSecurityHeaderType GetRequesterCredentials()
        {
            return new CustomSecurityHeaderType
                       {
                           Credentials = _payPalExpressCheckoutPaymentSettings.DoNotHaveBusinessAccount
                                             ? new UserIdPasswordType
                                                   {
                                                       Subject = _payPalExpressCheckoutPaymentSettings.EmailAddress,
                                                   }
                                             : new UserIdPasswordType
                                                   {
                                                       Signature = _payPalExpressCheckoutPaymentSettings.ApiSignature,
                                                       Username = _payPalExpressCheckoutPaymentSettings.Username,
                                                       Password = _payPalExpressCheckoutPaymentSettings.Password,
                                                   }
                       };
        }
    }
}