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
    public class PayPalCartItemService
    {
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IProductService _productService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ITaxService _taxService;
        private readonly IWorkContext _workContext;
        private readonly PayPalCurrencyCodeParser _payPalCurrencyCodeParser;

        public PayPalCartItemService(IOrderTotalCalculationService orderTotalCalculationService,
            IProductService productService,
            IShoppingCartService shoppingCartService,
            ITaxService taxService,
            IWorkContext workContext,
            PayPalCurrencyCodeParser payPalCurrencyCodeParser)
        {
            _orderTotalCalculationService = orderTotalCalculationService;
            _productService = productService;
            _shoppingCartService = shoppingCartService;
            _taxService = taxService;
            _workContext = workContext;
            _payPalCurrencyCodeParser = payPalCurrencyCodeParser;
        }

        public decimal GetCartItemTotal(IList<ShoppingCartItem> cart)
        {
            _orderTotalCalculationService.GetShoppingCartSubTotal(cart, false, out _, out _, out _, out var subTotalWithDiscount);
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
            return _orderTotalCalculationService.GetShoppingCartShippingTotal(cart).GetValueOrDefault();
        }

        public PaymentDetailsItemType CreatePaymentItem(ShoppingCartItem item)
        {
            var product = _productService.GetProductById(item.ProductId);

            if (product is null)
                throw new NopException("Product is not found");

            var productPrice = _taxService.GetProductPrice(product,
                _shoppingCartService.GetUnitPrice(item), false,
                _workContext.CurrentCustomer, out _);

            var currencyCodeType = _payPalCurrencyCodeParser.GetCurrencyCodeType(_workContext.WorkingCurrency);
            var paymentDetailsItemType = new PaymentDetailsItemType
            {
                Name = product.Name,
                //Description = _productAttributeFormatter.FormatAttributes(item.ProductVariant, item.AttributesXml),
                Amount = productPrice.GetBasicAmountType(currencyCodeType),
                ItemCategory =
                    product.IsDownload
                        ? ItemCategoryType.Digital
                        : ItemCategoryType.Physical,
                Quantity = item.Quantity.ToString()
            };

            return paymentDetailsItemType;
        }

        public decimal GetCartTotal(IList<ShoppingCartItem> cart, out decimal orderTotalDiscountAmount,
            out List<Discount> appliedDiscounts,
            out int redeemedRewardPoints, out decimal redeemedRewardPointsAmount,
            out List<AppliedGiftCard> appliedGiftCards)
        {
            _orderTotalCalculationService.GetShoppingCartTotal(cart, out orderTotalDiscountAmount,
                out appliedDiscounts,
                out appliedGiftCards, out redeemedRewardPoints,
                out redeemedRewardPointsAmount);

            return GetCartTotal(cart) - (orderTotalDiscountAmount + appliedGiftCards.Sum(x => x.AmountCanBeUsed));
        }

        public decimal GetCartItemTotal(IList<ShoppingCartItem> cart, out decimal subTotalDiscountAmount,
            out List<Discount> subTotalAppliedDiscounts, out decimal subTotalWithoutDiscount,
            out decimal subTotalWithDiscount)
        {
            _orderTotalCalculationService.GetShoppingCartSubTotal(cart, false, out subTotalDiscountAmount,
                out subTotalAppliedDiscounts,
                out subTotalWithoutDiscount, out subTotalWithDiscount);

            return subTotalWithDiscount;
        }
    }
}