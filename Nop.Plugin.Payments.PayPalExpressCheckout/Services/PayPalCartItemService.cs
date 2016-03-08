using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Catalog;
using Nop.Services.Orders;
using Nop.Services.Tax;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalCartItemService : IPayPalCartItemService
    {
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPayPalCurrencyCodeParser _payPalCurrencyCodeParser;
        private readonly ITaxService _taxService;
        private readonly IWorkContext _workContext;
        private readonly IPriceCalculationService _priceCalculationService;

        public PayPalCartItemService(IOrderTotalCalculationService orderTotalCalculationService,
                                     IPayPalCurrencyCodeParser payPalCurrencyCodeParser,
                                     ITaxService taxService,
                                     IWorkContext workContext,
                                     IPriceCalculationService priceCalculationService)
        {
            _orderTotalCalculationService = orderTotalCalculationService;
            _payPalCurrencyCodeParser = payPalCurrencyCodeParser;
            _taxService = taxService;
            _workContext = workContext;
            _priceCalculationService = priceCalculationService;
        }

        public decimal GetCartItemTotal(IList<ShoppingCartItem> cart)
        {
            decimal discountAmount;
            Discount appliedDiscount;
            decimal subTotalWithoutDiscount;
            decimal subTotalWithDiscount;
            _orderTotalCalculationService.GetShoppingCartSubTotal(cart, false, out discountAmount, out appliedDiscount,
                                                                  out subTotalWithoutDiscount, out subTotalWithDiscount);
            return subTotalWithDiscount;
        }

        public decimal GetCartTotal(IList<ShoppingCartItem> cart)
        {
            return GetCartItemTotal(cart) + GetTax(cart) + GetShippingTotal(cart);
        }

        public decimal GetTax(IList<ShoppingCartItem> cart)
        {
            return _orderTotalCalculationService.GetTaxTotal(cart);
        }

        public decimal GetShippingTotal(IList<ShoppingCartItem> cart)
        {
            return _orderTotalCalculationService.GetShoppingCartShippingTotal(cart, true).GetValueOrDefault();
        }

        public PaymentDetailsItemType CreatePaymentItem(ShoppingCartItem item)
        {
            decimal taxRate;
            var productPrice = _taxService.GetProductPrice(item.Product,
                                                           _priceCalculationService.GetUnitPrice(item, true), false,
                                                           _workContext.CurrentCustomer, out taxRate);

            var currencyCodeType = _payPalCurrencyCodeParser.GetCurrencyCodeType(_workContext.WorkingCurrency);
            var paymentDetailsItemType = new PaymentDetailsItemType
                                             {
                                                 Name = item.Product.Name,
                                                 //Description = _productAttributeFormatter.FormatAttributes(item.ProductVariant, item.AttributesXml),
                                                 Amount = productPrice.GetBasicAmountType(currencyCodeType),
                                                 ItemCategory =
                                                     item.Product.IsDownload
                                                         ? ItemCategoryType.Digital
                                                         : ItemCategoryType.Physical,
                                                 Quantity = item.Quantity.ToString()
                                             };
            return paymentDetailsItemType;
        }

        public decimal GetCartTotal(IList<ShoppingCartItem> cart, out decimal orderTotalDiscountAmount, out Discount appliedDiscount,
                                    out int redeemedRewardPoints, out decimal redeemedRewardPointsAmount, out List<AppliedGiftCard> appliedGiftCards)
        {
            _orderTotalCalculationService.GetShoppingCartTotal(cart, out orderTotalDiscountAmount,
                                                               out appliedDiscount,
                                                               out appliedGiftCards, out redeemedRewardPoints,
                                                               out redeemedRewardPointsAmount);

            return GetCartTotal(cart) - (orderTotalDiscountAmount + appliedGiftCards.Sum(x => x.AmountCanBeUsed));
        }

        public decimal GetCartItemTotal(IList<ShoppingCartItem> cart, out decimal subTotalDiscountAmount, out Discount subTotalAppliedDiscount, out decimal subTotalWithoutDiscount, out decimal subTotalWithDiscount)
        {
            _orderTotalCalculationService.GetShoppingCartSubTotal(cart, false, out subTotalDiscountAmount,
                                                                  out subTotalAppliedDiscount,
                                                                  out subTotalWithoutDiscount, out subTotalWithDiscount);
            return subTotalWithDiscount;
        }
    }
}