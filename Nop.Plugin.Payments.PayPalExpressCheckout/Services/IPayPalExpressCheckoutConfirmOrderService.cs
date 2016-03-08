using System.Collections.Generic;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalExpressCheckoutConfirmOrderService
    {
        CheckoutConfirmModel PrepareConfirmOrderModel(IList<ShoppingCartItem> cart);
    }
}