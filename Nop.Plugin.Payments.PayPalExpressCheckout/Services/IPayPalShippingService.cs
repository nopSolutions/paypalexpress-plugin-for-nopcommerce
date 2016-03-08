using System.Collections.Generic;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalShippingService
    {
        string GetRequireConfirmedShippingAddress(IEnumerable<ShoppingCartItem> cart);
        string GetNoShipping(IEnumerable<ShoppingCartItem> cart);
    }
}