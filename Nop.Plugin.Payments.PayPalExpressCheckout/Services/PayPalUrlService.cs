using System;
using Nop.Core;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalUrlService : IPayPalUrlService
    {
        private readonly IStoreContext _storeContext;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalUrlService(IStoreContext storeContext, PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _storeContext = storeContext;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public string GetReturnURL()
        {
            return
                new Uri(new Uri(_storeContext.CurrentStore.Url), "Plugins/PaymentPayPalExpressCheckout/ReturnHandler")
                    .ToString();
        }

        public string GetCancelURL()
        {
            return new Uri(new Uri(_storeContext.CurrentStore.Url), "cart").ToString();
        }

        public string GetCallbackURL()
        {
            return
                new Uri(new Uri(_storeContext.CurrentStore.Url), "Plugins/PaymentPayPalExpressCheckout/CallbackHandler")
                    .ToString();
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