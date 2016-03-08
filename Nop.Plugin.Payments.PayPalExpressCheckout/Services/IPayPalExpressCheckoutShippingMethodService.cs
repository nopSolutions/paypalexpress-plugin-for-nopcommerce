using System.Collections.Generic;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalExpressCheckoutShippingMethodService
    {
        CheckoutShippingMethodModel PrepareShippingMethodModel(IList<ShoppingCartItem> cart);
        bool SetShippingMethod(IList<ShoppingCartItem> cart, string shippingoption);
        void SetShippingMethodToNull();
    }
}