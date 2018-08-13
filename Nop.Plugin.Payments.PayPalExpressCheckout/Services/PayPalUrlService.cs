using System;
using Nop.Core;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalUrlService : IPayPalUrlService
    {
        private readonly IWebHelper _webHelper;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalUrlService(IWebHelper webHelper,
            PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _webHelper = webHelper;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public string GetReturnURL()
        {
            return string.Format("{0}Plugins/PaymentPayPalExpressCheckout/ReturnHandler", _webHelper.GetStoreLocation());
        }

        public string GetCancelURL()
        {
            return string.Format("{0}cart", _webHelper.GetStoreLocation());
        }

        public string GetCallbackURL()
        {
            throw new NotImplementedException();
        }

        public string GetCallbackTimeout()
        {
            return "5";
        }

        public string GetExpressCheckoutRedirectUrl(string token)
        {
            return
                string.Format(
                    _payPalExpressCheckoutPaymentSettings.IsLive
                        ? "https://www.paypal.com/webscr?cmd=_express-checkout&token={0}"
                        : "https://www.sandbox.paypal.com/webscr?cmd=_express-checkout&token={0}", token);
        }
    }
}