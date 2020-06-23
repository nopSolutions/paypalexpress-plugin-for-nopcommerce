using System;
using Nop.Core;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalUrlService
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
            return $"{_webHelper.GetStoreLocation()}Plugins/PaymentPayPalExpressCheckout/ReturnHandler";
        }

        public string GetCancelURL()
        {
            return $"{_webHelper.GetStoreLocation()}cart";
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