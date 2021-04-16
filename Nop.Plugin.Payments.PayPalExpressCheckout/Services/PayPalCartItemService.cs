using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<PaymentDetailsItemType> CreatePaymentItemAsync(ShoppingCartItem item)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);

            if (product is null)
                throw new NopException("Product is not found");

            var (productPrice, _) = await _taxService.GetProductPriceAsync(product,
                (await _shoppingCartService.GetUnitPriceAsync(item, true)).unitPrice, false,
                await _workContext.GetCurrentCustomerAsync());

            var currencyCodeType = _payPalCurrencyCodeParser.GetCurrencyCodeType(await _workContext.GetWorkingCurrencyAsync());
            var paymentDetailsItemType = new PaymentDetailsItemType
            {
                Name = product.Name,
                //Description = _productAttributeFormatter.FormatAttributes(item.ProductVariant, item.AttributesXml),
                Amount = await productPrice.GetBasicAmountTypeAsync(currencyCodeType),
                ItemCategory =
                    product.IsDownload
                        ? ItemCategoryType.Digital
                        : ItemCategoryType.Physical,
                Quantity = item.Quantity.ToString()
            };

            return paymentDetailsItemType;
        }

        public async Task<decimal> GetTaxAsync(IList<ShoppingCartItem> cart)
        {
            return (await _orderTotalCalculationService.GetTaxTotalAsync(cart)).taxTotal;
        }

        public async Task<decimal> GetShippingTotalAsync(IList<ShoppingCartItem> cart)
        {
            return (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(cart)).GetValueOrDefault();
        }

        public async Task<(decimal cartTotal, decimal orderTotalDiscountAmount, List<Discount> appliedDiscounts, int redeemedRewardPoints, decimal redeemedRewardPointsAmount, List<AppliedGiftCard> appliedGiftCards)> GetCartTotalAsync(IList<ShoppingCartItem> cart)
        {
            var (_, orderTotalDiscountAmount, appliedDiscounts, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

            var taxTotal = await GetTaxAsync(cart);
            var shippingTotal = await GetShippingTotalAsync(cart);
            var (_, _, _, subTotalWithDiscount, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false);

            var cartTotal = subTotalWithDiscount + taxTotal + shippingTotal;

            return (cartTotal - (orderTotalDiscountAmount + appliedGiftCards.Sum(x => x.AmountCanBeUsed)), orderTotalDiscountAmount, appliedDiscounts, redeemedRewardPoints, redeemedRewardPointsAmount, appliedGiftCards);
        }

        public async Task<(decimal cartItemTotal, decimal subTotalDiscountAmount, List<Discount> subTotalAppliedDiscounts, decimal subTotalWithoutDiscount, decimal subTotalWithDiscount)> GetCartItemTotalAsync(IList<ShoppingCartItem> cart)
        {
            var (subTotalDiscountAmount, subTotalAppliedDiscounts, subTotalWithoutDiscount, subTotalWithDiscount, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false);

            return (subTotalWithDiscount, subTotalDiscountAmount, subTotalAppliedDiscounts, subTotalWithoutDiscount, subTotalWithDiscount);
        }
    }
}