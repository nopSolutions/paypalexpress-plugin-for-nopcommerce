using System.Collections.Generic;
using System.Linq;
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

        public PaymentDetailsType[] GetPaymentDetails(IList<ShoppingCartItem> cart)
        {
            var currencyCode = _payPalCurrencyCodeParser.GetCurrencyCodeType(_workContext.WorkingCurrency);

            var orderTotalWithDiscount = _payPalCartItemService.GetCartTotal(cart, out var orderTotalDiscountAmount,
                out _,
                out _,
                out _,
                out var appliedGiftCards);

            var itemTotalWithDiscount = _payPalCartItemService.GetCartItemTotal(cart,
                out var subTotalDiscountAmount,
                out _,
                out _,
                out _);

            var giftCardsAmount = appliedGiftCards.Sum(x => x.AmountCanBeUsed);

            itemTotalWithDiscount = itemTotalWithDiscount - orderTotalDiscountAmount - giftCardsAmount;

            var taxTotal = _payPalCartItemService.GetTax(cart);
            var shippingTotal = _payPalCartItemService.GetShippingTotal(cart);
            var items = GetPaymentDetailsItems(cart);

            // checkout attributes
            if (_workContext.CurrentCustomer is Customer customer)
            {
                var checkoutAttributesXml = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.CheckoutAttributes, _storeContext.CurrentStore.Id);
                var caValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(checkoutAttributesXml);

                foreach (var (attribute, values) in caValues)
                {
                    foreach (var attributeValue in values)
                    {
                        if (attributeValue.PriceAdjustment <= 0)
                            continue;

                        var checkoutAttrItem = new PaymentDetailsItemType
                        {
                            Name = attributeValue.Name,
                            Amount = attributeValue.PriceAdjustment.GetBasicAmountType(currencyCode),
                            Quantity = "1"
                        };

                        items.Add(checkoutAttrItem);
                    }
                }
            }

            if (orderTotalDiscountAmount > 0 || subTotalDiscountAmount > 0)
            {
                var discountItem = new PaymentDetailsItemType
                {
                    Name = "Discount",
                    Amount = (-orderTotalDiscountAmount + -subTotalDiscountAmount).GetBasicAmountType(currencyCode),
                    Quantity = "1"
                };

                items.Add(discountItem);
            }

            foreach (var appliedGiftCard in appliedGiftCards)
            {
                var giftCardItem = new PaymentDetailsItemType
                {
                    Name = $"Gift Card ({appliedGiftCard.GiftCard.GiftCardCouponCode})",
                    Amount = (-appliedGiftCard.AmountCanBeUsed).GetBasicAmountType(currencyCode),
                    Quantity = "1"
                };

                items.Add(giftCardItem);
            }

            return new[]
            {
                new PaymentDetailsType
                    {
                        OrderTotal = orderTotalWithDiscount.GetBasicAmountType(currencyCode),
                        ItemTotal = itemTotalWithDiscount.GetBasicAmountType(currencyCode),
                        TaxTotal = taxTotal.GetBasicAmountType(currencyCode),
                        ShippingTotal = shippingTotal.GetBasicAmountType(currencyCode),
                        PaymentDetailsItem = items.ToArray(),
                        PaymentAction = _payPalExpressCheckoutPaymentSettings.PaymentAction,
                        PaymentActionSpecified = true,
                        ButtonSource = PayPalHelper.BnCode
                    }
            };
        }

        public BasicAmountType GetMaxAmount(IList<ShoppingCartItem> cart)
        {
            var getShippingOptionResponse = _shippingService.GetShippingOptions(cart, _addressService.GetAddressById(_workContext.CurrentCustomer.ShippingAddressId ?? 0));
            decimal toAdd = 0;

            if (getShippingOptionResponse.ShippingOptions != null && getShippingOptionResponse.ShippingOptions.Any())
                toAdd = getShippingOptionResponse.ShippingOptions.Max(option => option.Rate);

            var currencyCode = _payPalCurrencyCodeParser.GetCurrencyCodeType(_workContext.WorkingCurrency);
            var cartTotal = _payPalCartItemService.GetCartItemTotal(cart);

            return (cartTotal + toAdd).GetBasicAmountType(currencyCode);
        }

        private IList<PaymentDetailsItemType> GetPaymentDetailsItems(IEnumerable<ShoppingCartItem> cart)
        {
            return cart.Select(item => _payPalCartItemService.CreatePaymentItem(item)).ToList();
        }

        public string GetBuyerEmail()
        {
            return _workContext.CurrentCustomer?.Email;
        }
    }
}