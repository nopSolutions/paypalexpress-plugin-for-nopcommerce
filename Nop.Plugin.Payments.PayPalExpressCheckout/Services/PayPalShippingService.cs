using System.Collections.Generic;
using System.Linq;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalShippingService : IPayPalShippingService
    {
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalShippingService(PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public string GetRequireConfirmedShippingAddress(IEnumerable<ShoppingCartItem> cart)
        {
            return cart.All(item => item.Product.IsDownload)
                       ? "0"
                       : _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress
                             ? "1"
                             : "0";
        }

        public string GetNoShipping(IEnumerable<ShoppingCartItem> cart)
        {
            return cart.Any(item => !item.Product.IsDownload)
                       ? "2"
                       : "1";
        }
    }
}