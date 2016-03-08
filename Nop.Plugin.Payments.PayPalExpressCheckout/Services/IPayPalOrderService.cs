using System.Collections.Generic;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalOrderService
    {
        PaymentDetailsType[] GetPaymentDetails(IList<ShoppingCartItem> cart);
        BasicAmountType GetMaxAmount(IList<ShoppingCartItem> cart);
        string GetBuyerEmail();
    }
}