using System.Collections.Generic;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalCartItemService
    {
        decimal GetCartItemTotal(IList<ShoppingCartItem> cart);
        decimal GetCartTotal(IList<ShoppingCartItem> cart);
        decimal GetTax(IList<ShoppingCartItem> cart);
        decimal GetShippingTotal(IList<ShoppingCartItem> cart);
        PaymentDetailsItemType CreatePaymentItem(ShoppingCartItem item);

        decimal GetCartTotal(IList<ShoppingCartItem> cart, out decimal orderTotalDiscountAmount,
                             out Discount appliedDiscount, out int redeemedRewardPoints,
                             out decimal redeemedRewardPointsAmount, out List<AppliedGiftCard> appliedGiftCards);

        decimal GetCartItemTotal(IList<ShoppingCartItem> cart, out decimal subTotalDiscountAmount, out Discount subTotalAppliedDiscount, out decimal subTotalWithoutDiscount, out decimal subTotalWithDiscount);
    }
}