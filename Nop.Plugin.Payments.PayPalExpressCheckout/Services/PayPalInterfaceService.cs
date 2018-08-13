using System.ServiceModel;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalInterfaceService : IPayPalInterfaceService
    {
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalInterfaceService(PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public PayPalAPIAAInterfaceClient GetAAService()
        {
            return
                new PayPalAPIAAInterfaceClient(new BasicHttpsBinding(),
                                               new EndpointAddress(_payPalExpressCheckoutPaymentSettings.IsLive
                                                                       ? "https://api-3t.paypal.com/2.0/"
                                                                       : "https://api-3t.sandbox.paypal.com/2.0/"));
        }

        public PayPalAPIInterfaceClient GetService()
        {
            return
                new PayPalAPIInterfaceClient(new BasicHttpsBinding(),
                                               new EndpointAddress(_payPalExpressCheckoutPaymentSettings.IsLive
                                                                       ? "https://api-3t.paypal.com/2.0/"
                                                                       : "https://api-3t.sandbox.paypal.com/2.0/"));
        }
    }
}