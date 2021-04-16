using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Common;
using Nop.Services.Orders;
using Nop.Services.Shipping;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalOrderService
    {
        private readonly IAddressService _addressService;
        private readonly IShippingService _shippingService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IStoreContext _storeContext;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly IWorkContext _workContext;
        private readonly PayPalCartItemService _payPalCartItemService;
        private readonly PayPalCurrencyCodeParser _payPalCurrencyCodeParser;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalOrderService(
            IAddressService addressService,
            IShippingService shippingService,
            IGenericAttributeService genericAttributeService,
            IStoreContext storeContext,
            ICheckoutAttributeParser checkoutAttributeParser,
            IWorkContext workContext,
            PayPalCartItemService payPalCartItemService,
            PayPalCurrencyCodeParser payPalCurrencyCodeParser,
            PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _addressService = addressService;
            _shippingService = shippingService;
            _genericAttributeService = genericAttributeService;
            _storeContext = storeContext;
            _checkoutAttributeParser = checkoutAttributeParser;
            _workContext = workContext;
            _payPalCartItemService = payPalCartItemService;
            _payPalCurrencyCodeParser = payPalCurrencyCodeParser;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public async Task<PaymentDetailsType[]> GetPaymentDetailsAsync(IList<ShoppingCartItem> cart)
        {
            var currencyCode = _payPalCurrencyCodeParser.GetCurrencyCodeType(await _workContext.GetWorkingCurrencyAsync());

            var (orderTotalWithDiscount, orderTotalDiscountAmount, _, _, _, appliedGiftCards) = await _payPalCartItemService.GetCartTotalAsync(cart);

            var (itemTotalWithDiscount, subTotalDiscountAmount, _, _, _) = await _payPalCartItemService.GetCartItemTotalAsync(cart);

            var giftCardsAmount = appliedGiftCards.Sum(x => x.AmountCanBeUsed);

            itemTotalWithDiscount = itemTotalWithDiscount - orderTotalDiscountAmount - giftCardsAmount;

            var taxTotal = await _payPalCartItemService.GetTaxAsync(cart);
            var shippingTotal = await _payPalCartItemService.GetShippingTotalAsync(cart);
            var items = await GetPaymentDetailsItemsAsync(cart);

            var customer = await _workContext.GetCurrentCustomerAsync();
            // checkout attributes
            if (customer != null)
            {
                var checkoutAttributesXml = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, (await _storeContext.GetCurrentStoreAsync()).Id);
                var caValues = await _checkoutAttributeParser.ParseCheckoutAttributeValues(checkoutAttributesXml).ToListAsync();

                foreach (var (_, values) in caValues)
                foreach (var attributeValue in await values.ToListAsync())
                {
                    if (attributeValue.PriceAdjustment <= 0)
                        continue;

                    var checkoutAttrItem = new PaymentDetailsItemType
                    {
                        Name = attributeValue.Name,
                        Amount = await attributeValue.PriceAdjustment.GetBasicAmountTypeAsync(currencyCode),
                        Quantity = "1"
                    };

                    items.Add(checkoutAttrItem);
                }
            }

            if (orderTotalDiscountAmount > 0 || subTotalDiscountAmount > 0)
            {
                var discountItem = new PaymentDetailsItemType
                {
                    Name = "Discount",
                    Amount = await (-orderTotalDiscountAmount + -subTotalDiscountAmount).GetBasicAmountTypeAsync(currencyCode),
                    Quantity = "1"
                };

                items.Add(discountItem);
            }

            foreach (var appliedGiftCard in appliedGiftCards)
            {
                var giftCardItem = new PaymentDetailsItemType
                {
                    Name = $"Gift Card ({appliedGiftCard.GiftCard.GiftCardCouponCode})",
                    Amount = await (-appliedGiftCard.AmountCanBeUsed).GetBasicAmountTypeAsync(currencyCode),
                    Quantity = "1"
                };

                items.Add(giftCardItem);
            }

            return new[]
            {
                new PaymentDetailsType
                    {
                        OrderTotal = await orderTotalWithDiscount.GetBasicAmountTypeAsync(currencyCode),
                        ItemTotal = await itemTotalWithDiscount.GetBasicAmountTypeAsync(currencyCode),
                        TaxTotal = await taxTotal.GetBasicAmountTypeAsync(currencyCode),
                        ShippingTotal = await shippingTotal.GetBasicAmountTypeAsync(currencyCode),
                        PaymentDetailsItem = items.ToArray(),
                        PaymentAction = _payPalExpressCheckoutPaymentSettings.PaymentAction,
                        PaymentActionSpecified = true,
                        ButtonSource = PayPalHelper.BnCode
                    }
            };
        }

        public async Task<BasicAmountType> GetMaxAmountAsync(IList<ShoppingCartItem> cart)
        {
            var getShippingOptionResponse = await _shippingService.GetShippingOptionsAsync(cart, await _addressService.GetAddressByIdAsync((await _workContext.GetCurrentCustomerAsync()).ShippingAddressId ?? 0));
            decimal toAdd = 0;

            if (getShippingOptionResponse.ShippingOptions != null && getShippingOptionResponse.ShippingOptions.Any())
                toAdd = getShippingOptionResponse.ShippingOptions.Max(option => option.Rate);

            var currencyCode = _payPalCurrencyCodeParser.GetCurrencyCodeType(await _workContext.GetWorkingCurrencyAsync());
            var (cartTotal, _, _, _, _) = await _payPalCartItemService.GetCartItemTotalAsync(cart);

            return await (cartTotal + toAdd).GetBasicAmountTypeAsync(currencyCode);
        }

        private async Task<IList<PaymentDetailsItemType>> GetPaymentDetailsItemsAsync(IEnumerable<ShoppingCartItem> cart)
        {
            return await cart.SelectAwait(async item => await _payPalCartItemService.CreatePaymentItemAsync(item)).ToListAsync();
        }

        public async Task<string> GetBuyerEmailAsync()
        {
            return (await _workContext.GetCurrentCustomerAsync())?.Email;
        }
    }
}