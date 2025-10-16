using System.Globalization;
using System.ServiceModel;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Http;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services;

public class PayPalExpressCheckoutService
{
    #region Fields

    private readonly CurrencySettings _currencySettings;
    private readonly IAddressService _addressService;
    private readonly IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> _checkoutAttributeParser;
    private readonly ICountryService _countryService;
    private readonly ICurrencyService _currencyService;
    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderService _orderService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly IPaymentService _paymentService;
    private readonly IPriceFormatter _priceFormatter;
    private readonly IProductService _productService;
    private readonly IShippingService _shippingService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IStateProvinceService _stateProvinceService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly ITaxService _taxService;
    private readonly IWebHelper _webHelper;
    private readonly IWorkContext _workContext;
    private readonly OrderSettings _orderSettings;
    private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

    #endregion

    #region Ctor

    public PayPalExpressCheckoutService(CurrencySettings currencySettings,
        IAddressService addressService,
        IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        ILogger logger,
        INopUrlHelper nopUrlHelper,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IPaymentService paymentService,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IShippingService shippingService,
        IShoppingCartService shoppingCartService,
        IStateProvinceService stateProvinceService,
        IStoreContext storeContext,
        IStoreService storeService,
        ITaxService taxService,
        IWebHelper webHelper,
        IWorkContext workContext,
        OrderSettings orderSettings,
        PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
    {
        _currencySettings = currencySettings;
        _addressService = addressService;
        _checkoutAttributeParser = checkoutAttributeParser;
        _countryService = countryService;
        _currencyService = currencyService;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
        _logger = logger;
        _nopUrlHelper = nopUrlHelper;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _paymentService = paymentService;
        _priceFormatter = priceFormatter;
        _productService = productService;
        _shippingService = shippingService;
        _shoppingCartService = shoppingCartService;
        _stateProvinceService = stateProvinceService;
        _storeContext = storeContext;
        _storeService = storeService;
        _taxService = taxService;
        _webHelper = webHelper;
        _workContext = workContext;
        _orderSettings = orderSettings;
        _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
    }

    #endregion

    #region Utilities

    #region Common

    private string GetVersion()
    {
        return "98.0";
    }

    private async Task<BasicAmountType> GetBasicAmountTypeAsync(decimal value, CurrencyCodeType currencyCode)
    {
        var currency = await _workContext.GetWorkingCurrencyAsync();

        return new()
        {
            currencyID = currencyCode,
            Value = Math.Round(_currencyService.ConvertCurrency(value, currency.Rate), 2).ToString("N", CultureInfo.InvariantCulture)
        };
    }

    private async Task LogResponseAsync<TResponseType>(PayPalExpressCheckoutPaymentSettings settings, TResponseType response, Order order = null)
        where TResponseType : AbstractResponseType
    {
        if (order is null && !settings.EnableDebugLogging)
            return;

        var fullMessage = JsonConvert.SerializeObject(response);
        var chunks = fullMessage.ToList().Chunk(3500).ToList();
        for (var index = 0; index < chunks.Count; index++)
        {
            if (order is not null)
            {
                await _orderService.InsertOrderNoteAsync(new()
                {
                    OrderId = order.Id,
                    DisplayToCustomer = false,
                    Note = $"{response.GetType().Name} returned - Part {index + 1} of {chunks.Count}" + " - " + new string(chunks[index]),
                    CreatedOnUtc = DateTime.UtcNow
                });
            }

            if (settings.EnableDebugLogging)
                await _logger.InsertLogAsync(LogLevel.Debug, $"{response.GetType().Name} returned - Part {index + 1} of {chunks.Count}", new string(chunks[index]));
        }
    }

    private CurrencyCodeType GetCurrencyCodeType(string currencyCode)
    {
        return !string.IsNullOrEmpty(currencyCode) && Enum.TryParse(currencyCode, out CurrencyCodeType code)
            ? code
            : CurrencyCodeType.CustomCode;
    }

    #endregion

    #region Checkout

    private async Task<bool> IsMinimumOrderPlacementIntervalValidAsync()
    {
        //prevent 2 orders being placed within an X seconds time frame
        if (_orderSettings.MinimumOrderPlacementInterval == 0)
            return true;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var lastOrder = (await _orderService.SearchOrdersAsync(storeId: store.Id, customerId: customer.Id, pageSize: 1)).FirstOrDefault();
        if (lastOrder == null)
            return true;

        var interval = DateTime.UtcNow - lastOrder.CreatedOnUtc;
        return interval.TotalMinutes > _orderSettings.MinimumOrderPlacementInterval;
    }

    private async Task<AddressType> GetAddressAsync(Customer customer)
    {
        var address = await _addressService.GetAddressByIdAsync(customer.BillingAddressId ?? 0);
        if (address is null)
            return null;

        var country = await _countryService.GetCountryByIdAsync(address.CountryId ?? 0);
        var state = await _stateProvinceService.GetStateProvinceByIdAsync(address.StateProvinceId ?? 0);
        return new()
        {
            Street1 = address.Address1,
            Street2 = address.Address2,
            StateOrProvince = state?.Abbreviation,
            Country = Enum.TryParse<CountryCodeType>(country?.TwoLetterIsoCode, out var billingCountry)
                ? billingCountry
                : CountryCodeType.US,
            PostalCode = address.ZipPostalCode,
            Phone = address.PhoneNumber
        };
    }

    private async Task<ProcessPaymentRequest> SetCheckoutDetailsAsync(GetExpressCheckoutDetailsResponseDetailsType checkoutDetails)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        // get/update billing address
        var billingFirstName = checkoutDetails.PayerInfo?.PayerName?.FirstName;
        var billingLastName = checkoutDetails.PayerInfo?.PayerName?.LastName;
        var billingEmail = checkoutDetails.PayerInfo?.Payer;
        var billingAddress1 = checkoutDetails.BillingAddress?.Street1 ?? checkoutDetails.PayerInfo?.Address?.Street1;
        var billingAddress2 = checkoutDetails.BillingAddress?.Street2 ?? checkoutDetails.PayerInfo?.Address?.Street2;
        var billingPhoneNumber = checkoutDetails.BillingAddress?.Phone ?? checkoutDetails.PayerInfo?.ContactPhone;
        var billingCity = checkoutDetails.BillingAddress?.CityName ?? checkoutDetails.PayerInfo?.Address?.CityName;
        int? billingStateProvinceId = null;
        var billingStateProvince = await _stateProvinceService.GetStateProvinceByAbbreviationAsync(checkoutDetails.BillingAddress?.StateOrProvince)
            ?? await _stateProvinceService.GetStateProvinceByAbbreviationAsync(checkoutDetails.PayerInfo?.Address?.StateOrProvince);
        if (billingStateProvince != null)
            billingStateProvinceId = billingStateProvince.Id;
        var billingZipPostalCode = checkoutDetails.BillingAddress?.PostalCode ?? checkoutDetails.PayerInfo?.Address?.PostalCode;
        int? billingCountryId = null;
        var billingCountry = await _countryService.GetCountryByTwoLetterIsoCodeAsync(checkoutDetails.BillingAddress?.Country.ToString())
             ?? await _countryService.GetCountryByTwoLetterIsoCodeAsync(checkoutDetails.PayerInfo?.Address?.Country.ToString());
        if (billingCountry != null)
            billingCountryId = billingCountry.Id;

        var billingAddress = _addressService.FindAddress((await _customerService.GetAddressesByCustomerIdAsync(customer.Id)).ToList(),
            billingFirstName, billingLastName, billingPhoneNumber,
            billingEmail, string.Empty, string.Empty,
            billingAddress1, billingAddress2, billingCity,
            billingCountry?.Name, billingStateProvinceId, billingZipPostalCode,
            billingCountryId, null);

        if (billingAddress == null)
        {
            billingAddress = new Core.Domain.Common.Address
            {
                FirstName = billingFirstName,
                LastName = billingLastName,
                PhoneNumber = billingPhoneNumber,
                Email = billingEmail,
                FaxNumber = string.Empty,
                Company = string.Empty,
                Address1 = billingAddress1,
                Address2 = billingAddress2,
                City = billingCity,
                StateProvinceId = billingStateProvinceId,
                ZipPostalCode = billingZipPostalCode,
                CountryId = billingCountryId,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _addressService.InsertAddressAsync(billingAddress);
            await _customerService.InsertCustomerAddressAsync(customer, billingAddress);
        }

        //set default billing address
        customer.BillingAddressId = billingAddress.Id;
        await _customerService.UpdateCustomerAsync(customer);

        await _genericAttributeService
            .SaveAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, store.Id);

        var cart = await GetCartAsync();
        var shoppingCartRequiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
        if (shoppingCartRequiresShipping)
        {
            var paymentDetails = checkoutDetails.PaymentDetails.FirstOrDefault() ?? new PaymentDetailsType();
            var shippingFullname = paymentDetails.ShipToAddress?.Name.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var shippingFirstName = shippingFullname.FirstOrDefault();
            var shippingLastName = string.Empty;
            if (shippingFullname.Length > 1)
                shippingLastName = shippingFullname.LastOrDefault();
            var shippingEmail = checkoutDetails.PayerInfo?.Payer;
            var shippingAddress1 = paymentDetails.ShipToAddress?.Street1;
            var shippingAddress2 = paymentDetails.ShipToAddress?.Street2;
            var shippingPhoneNumber = paymentDetails.ShipToAddress?.Phone;
            var shippingCity = paymentDetails.ShipToAddress?.CityName;
            int? shippingStateProvinceId = null;
            var shippingStateProvince = await _stateProvinceService.GetStateProvinceByAbbreviationAsync(paymentDetails.ShipToAddress?.StateOrProvince);
            if (shippingStateProvince != null)
                shippingStateProvinceId = shippingStateProvince.Id;
            int? shippingCountryId = null;
            var shippingZipPostalCode = paymentDetails.ShipToAddress?.PostalCode;
            var shippingCountry =
                await _countryService.GetCountryByTwoLetterIsoCodeAsync(paymentDetails.ShipToAddress?.Country.ToString());
            if (shippingCountry != null)
                shippingCountryId = shippingCountry.Id;

            var shippingAddress = _addressService.FindAddress((await _customerService.GetAddressesByCustomerIdAsync(customer.Id)).ToList(),
                shippingFirstName, shippingLastName, shippingPhoneNumber,
                shippingEmail, string.Empty, string.Empty,
                shippingAddress1, shippingAddress2, shippingCity,
                shippingCountry?.Name, shippingStateProvinceId, shippingZipPostalCode,
                shippingCountryId, null);

            if (shippingAddress == null)
            {
                shippingAddress = new Core.Domain.Common.Address
                {
                    FirstName = shippingFirstName,
                    LastName = shippingLastName,
                    PhoneNumber = shippingPhoneNumber,
                    Email = shippingEmail,
                    FaxNumber = string.Empty,
                    Company = string.Empty,
                    Address1 = shippingAddress1,
                    Address2 = shippingAddress2,
                    City = shippingCity,
                    StateProvinceId = shippingStateProvinceId,
                    ZipPostalCode = shippingZipPostalCode,
                    CountryId = shippingCountryId,
                    CreatedOnUtc = DateTime.UtcNow
                };

                await _addressService.InsertAddressAsync(shippingAddress);
                await _customerService.InsertCustomerAddressAsync(customer, shippingAddress);
            }

            //set default shipping address
            customer.ShippingAddressId = shippingAddress.Id;
            await _customerService.UpdateCustomerAsync(customer);
        }

        var processPaymentRequest = new ProcessPaymentRequest
        {
            CustomerId = customer.Id
        };
        processPaymentRequest.CustomValues.Add(new(Defaults.PaypalTokenKey, checkoutDetails.Token, displayToCustomer: false));
        processPaymentRequest.CustomValues.Add(new(Defaults.PaypalPayerIdKey, checkoutDetails.PayerInfo.PayerID, displayToCustomer: false));

        return processPaymentRequest;
    }

    private async Task<PaymentDetailsItemType> CreatePaymentItemAsync(ShoppingCartItem item)
    {
        var product = await _productService.GetProductByIdAsync(item.ProductId)
            ?? throw new NopException("Product is not found");

        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();

        var (unitPrice, _, _) = await _shoppingCartService.GetUnitPriceAsync(item, true);
        var (productPrice, _) = await _taxService.GetProductPriceAsync(product, unitPrice, false, customer);
        var currencyCodeType = GetCurrencyCodeType(currency.CurrencyCode);

        return new()
        {
            Name = product.Name,
            Amount = await GetBasicAmountTypeAsync(productPrice, currencyCodeType),
            ItemCategory = product.IsDownload ? ItemCategoryType.Digital : ItemCategoryType.Physical,
            Quantity = item.Quantity.ToString()
        };
    }

    private async Task<decimal> GetTaxAsync(IList<ShoppingCartItem> cart)
    {
        return (await _orderTotalCalculationService.GetTaxTotalAsync(cart)).taxTotal;
    }

    private async Task<decimal> GetShippingTotalAsync(IList<ShoppingCartItem> cart)
    {
        return (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(cart)).GetValueOrDefault();
    }

    private async Task<(decimal cartTotal, decimal orderTotalDiscountAmount, List<Discount> appliedDiscounts, int redeemedRewardPoints, decimal redeemedRewardPointsAmount, List<AppliedGiftCard> appliedGiftCards)> GetCartTotalAsync(IList<ShoppingCartItem> cart)
    {
        var (_, orderTotalDiscountAmount, appliedDiscounts, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

        var taxTotal = await GetTaxAsync(cart);
        var shippingTotal = await GetShippingTotalAsync(cart);
        var (_, _, _, subTotalWithDiscount, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false);

        var cartTotal = subTotalWithDiscount + taxTotal + shippingTotal;

        return (cartTotal - (orderTotalDiscountAmount + appliedGiftCards.Sum(x => x.AmountCanBeUsed)), orderTotalDiscountAmount, appliedDiscounts, redeemedRewardPoints, redeemedRewardPointsAmount, appliedGiftCards);
    }

    private async Task<(decimal cartItemTotal, decimal subTotalDiscountAmount, List<Discount> subTotalAppliedDiscounts, decimal subTotalWithoutDiscount, decimal subTotalWithDiscount)> GetCartItemTotalAsync(IList<ShoppingCartItem> cart)
    {
        var (subTotalDiscountAmount, subTotalAppliedDiscounts, subTotalWithoutDiscount, subTotalWithDiscount, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, false);

        return (subTotalWithDiscount, subTotalDiscountAmount, subTotalAppliedDiscounts, subTotalWithoutDiscount, subTotalWithDiscount);
    }

    private async Task<PaymentDetailsType[]> GetPaymentDetailsAsync(IList<ShoppingCartItem> cart)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var currencyCode = GetCurrencyCodeType(currency.CurrencyCode);
        var (orderTotalWithDiscount, orderTotalDiscountAmount, _, _, _, appliedGiftCards) = await GetCartTotalAsync(cart);
        var (itemTotalWithDiscount, subTotalDiscountAmount, _, _, _) = await GetCartItemTotalAsync(cart);

        var giftCardsAmount = appliedGiftCards.Sum(x => x.AmountCanBeUsed);
        itemTotalWithDiscount = itemTotalWithDiscount - orderTotalDiscountAmount - giftCardsAmount;

        var taxTotal = await GetTaxAsync(cart);
        var shippingTotal = await GetShippingTotalAsync(cart);
        var items = await GetPaymentDetailsItemsAsync(cart);

        // checkout attributes
        if (customer != null)
        {
            var checkoutAttributesXml = await _genericAttributeService
                .GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id);
            var caValues = await _checkoutAttributeParser.ParseAttributeValues(checkoutAttributesXml).ToListAsync();

            foreach (var (_, values) in caValues)
            {
                foreach (var attributeValue in await values.ToListAsync())
                {
                    if (attributeValue.PriceAdjustment <= 0)
                        continue;

                    var checkoutAttrItem = new PaymentDetailsItemType
                    {
                        Name = attributeValue.Name,
                        Amount = await GetBasicAmountTypeAsync(attributeValue.PriceAdjustment, currencyCode),
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
                Amount = await GetBasicAmountTypeAsync(-orderTotalDiscountAmount + -subTotalDiscountAmount, currencyCode),
                Quantity = "1"
            };

            items.Add(discountItem);
        }

        foreach (var appliedGiftCard in appliedGiftCards)
        {
            var giftCardItem = new PaymentDetailsItemType
            {
                Name = $"Gift Card ({appliedGiftCard.GiftCard.GiftCardCouponCode})",
                Amount = await GetBasicAmountTypeAsync(-appliedGiftCard.AmountCanBeUsed, currencyCode),
                Quantity = "1"
            };

            items.Add(giftCardItem);
        }

        return new[]
        {
            new PaymentDetailsType
            {
                OrderTotal = await GetBasicAmountTypeAsync(orderTotalWithDiscount, currencyCode),
                ItemTotal = await GetBasicAmountTypeAsync(itemTotalWithDiscount, currencyCode),
                TaxTotal = await GetBasicAmountTypeAsync(taxTotal, currencyCode),
                ShippingTotal = await GetBasicAmountTypeAsync(shippingTotal, currencyCode),
                PaymentDetailsItem = items.ToArray(),
                PaymentAction = GetPaymentAction(_payPalExpressCheckoutPaymentSettings),
                PaymentActionSpecified = true,
                ButtonSource = Defaults.PartnerCode,
                NotifyURL = _nopUrlHelper.RouteUrl(Defaults.Route.Ipn, null, _webHelper.GetCurrentRequestProtocol())
            }
        };
    }

    private async Task<BasicAmountType> GetMaxAmountAsync(IList<ShoppingCartItem> cart)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();

        var address = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId ?? 0);
        var getShippingOptionResponse = await _shippingService.GetShippingOptionsAsync(cart, address);

        var toAdd = 0M;

        if (getShippingOptionResponse.ShippingOptions != null && getShippingOptionResponse.ShippingOptions.Any())
            toAdd = getShippingOptionResponse.ShippingOptions.Max(option => option.Rate);

        var currencyCode = GetCurrencyCodeType(currency.CurrencyCode);
        var (cartTotal, _, _, _, _) = await GetCartItemTotalAsync(cart);

        return await GetBasicAmountTypeAsync(cartTotal + toAdd, currencyCode);
    }

    private async Task<IList<PaymentDetailsItemType>> GetPaymentDetailsItemsAsync(IEnumerable<ShoppingCartItem> cart)
    {
        return await cart.SelectAwait(async item => await CreatePaymentItemAsync(item)).ToListAsync();
    }

    #endregion

    #region Recurring payments

    private async Task<CreateRecurringPaymentsProfileRequestDetailsType> GetCreateRecurringPaymentProfileRequestDetailsAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var customer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId)
            ?? throw new NopException("Customer is not found");

        if ((customer.BillingAddressId ?? 0) == 0)
            throw new NopException("Customer doesn't have a billing address");

        var details = new CreateRecurringPaymentsProfileRequestDetailsType
        {
            Token = processPaymentRequest.CustomValues[Defaults.PaypalTokenKey]
        };

        var currency = await _workContext.GetWorkingCurrencyAsync();

        var billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId.Value);
        var country = await _countryService.GetCountryByAddressAsync(billingAddress);
        var payPalCountry = Enum.TryParse<CountryCodeType>(country?.TwoLetterIsoCode, out var payerCountry)
            ? payerCountry
            : CountryCodeType.US;

        details.CreditCard = new CreditCardDetailsType
        {
            CreditCardNumber = processPaymentRequest.CreditCardNumber,
            CreditCardType = Enum.TryParse<CreditCardTypeType>(processPaymentRequest.CreditCardType, out var creditCardTypeType)
                ? creditCardTypeType
                : CreditCardTypeType.Visa,
            ExpMonthSpecified = true,
            ExpMonth = processPaymentRequest.CreditCardExpireMonth,
            ExpYearSpecified = true,
            ExpYear = processPaymentRequest.CreditCardExpireYear,
            CVV2 = processPaymentRequest.CreditCardCvv2,
            CardOwner = new PayerInfoType { PayerCountry = payPalCountry },
            CreditCardTypeSpecified = true
        };

        details.CreditCard.CardOwner.Address = new AddressType
        {
            CountrySpecified = true,
            Street1 = billingAddress.Address1,
            Street2 = billingAddress.Address2,
            CityName = billingAddress.City,
            StateOrProvince = (await _stateProvinceService.GetStateProvinceByAddressAsync(billingAddress))?.Abbreviation ?? "CA",
            Country = payPalCountry,
            PostalCode = billingAddress.ZipPostalCode
        };
        details.CreditCard.CardOwner.Payer = billingAddress.Email;
        details.CreditCard.CardOwner.PayerName = new PersonNameType
        {
            FirstName = billingAddress.FirstName,
            LastName = billingAddress.LastName
        };

        //start date
        details.RecurringPaymentsProfileDetails = new RecurringPaymentsProfileDetailsType
        {
            BillingStartDate = DateTime.UtcNow,
            ProfileReference = processPaymentRequest.OrderGuid.ToString()
        };

        //schedule
        details.ScheduleDetails = new ScheduleDetailsType();
        var store = await _storeService.GetStoreByIdAsync(processPaymentRequest.StoreId);
        var storeName = store == null ? string.Empty : store.Name;
        details.ScheduleDetails.Description = $"{storeName} - recurring payment";
        var currencyCodeType = GetCurrencyCodeType(currency.CurrencyCode);

        details.ScheduleDetails.PaymentPeriod = new BillingPeriodDetailsType
        {
            Amount = await GetBasicAmountTypeAsync(processPaymentRequest.OrderTotal, currencyCodeType),
            BillingFrequency = processPaymentRequest.RecurringCycleLength,
            BillingPeriod = processPaymentRequest.RecurringCyclePeriod switch
            {
                RecurringProductCyclePeriod.Days => BillingPeriodType.Day,
                RecurringProductCyclePeriod.Weeks => BillingPeriodType.Week,
                RecurringProductCyclePeriod.Months => BillingPeriodType.Month,
                RecurringProductCyclePeriod.Years => BillingPeriodType.Year,
                _ => throw new NopException("Not supported cycle period"),
            },
            TotalBillingCycles = processPaymentRequest.RecurringTotalCycles,
            TotalBillingCyclesSpecified = true
        };

        return details;
    }

    #endregion

    #region Requests

    private async Task<SetExpressCheckoutRequestDetailsType> GetSetExpressCheckoutRequestDetailsAsync(IList<ShoppingCartItem> cart)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var noShippingCart = await cart.AnyAwaitAsync(async item => (await _productService.GetProductByIdAsync(item.ProductId))?.IsDownload != true);

        return new()
        {
            ReturnURL = _nopUrlHelper.RouteUrl(Defaults.Route.Return, null, _webHelper.GetCurrentRequestProtocol()),
            CancelURL = _nopUrlHelper.RouteUrl(NopRouteNames.General.CART, null, _webHelper.GetCurrentRequestProtocol()),
            ReqConfirmShipping = noShippingCart || !_payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress ? "0" : "1",
            NoShipping = noShippingCart ? "2" : "1",
            LocaleCode = _payPalExpressCheckoutPaymentSettings.LocaleCode,
            cppheaderimage = _payPalExpressCheckoutPaymentSettings.LogoImageURL,
            cppcartbordercolor = _payPalExpressCheckoutPaymentSettings.CartBorderColor,
            PaymentDetails = await GetPaymentDetailsAsync(cart),
            BuyerEmail = customer?.Email,
            Address = await GetAddressAsync(customer),
            BillingAddress = await GetAddressAsync(customer),
            MaxAmount = await GetMaxAmountAsync(cart)
        };
    }

    private async Task<SetExpressCheckoutReq> GetSetExpressCheckoutRequestAsync(IList<ShoppingCartItem> shoppingCartItems)
    {
        return new()
        {
            SetExpressCheckoutRequest = new()
            {
                SetExpressCheckoutRequestDetails = await GetSetExpressCheckoutRequestDetailsAsync(shoppingCartItems),
                Version = GetVersion()
            }
        };
    }

    private GetExpressCheckoutDetailsReq GetGetExpressCheckoutDetailsRequest(string token)
    {
        return new()
        {
            GetExpressCheckoutDetailsRequest = new()
            {
                Token = token,
                Version = GetVersion()
            }
        };
    }

    #endregion

    #endregion

    #region Methods

    #region Configuration

    public PaymentActionCodeType GetPaymentAction(PayPalExpressCheckoutPaymentSettings settings)
    {
        return Enum.TryParse<PaymentActionCodeType>(settings.PaymentAction, out var paymentAction)
            ? paymentAction
            : PaymentActionCodeType.Sale;
    }

    public string GetPaymentAction(int id)
    {
        return ((PaymentActionCodeType)id).ToString();
    }

    public PaymentStatus GetStatusByPaymentAction(PayPalExpressCheckoutPaymentSettings settings)
    {
        return GetPaymentAction(settings) == PaymentActionCodeType.Authorization
            ? PaymentStatus.Authorized
            : PaymentStatus.Paid;
    }

    public List<(string Text, string Value, bool Selected)> GetPaymentActionOptions(PayPalExpressCheckoutPaymentSettings settings = null)
    {
        var paymentAction = settings is not null
            ? GetPaymentAction(settings)
            : (PaymentActionCodeType?)null;

        return new List<(string Text, string Value, bool Selected)>()
        {
            (PaymentActionCodeType.Authorization.ToString(), ((int)PaymentActionCodeType.Authorization).ToString(), paymentAction == PaymentActionCodeType.Authorization),
            (PaymentActionCodeType.Sale.ToString(), ((int)PaymentActionCodeType.Sale).ToString(), paymentAction == PaymentActionCodeType.Sale),
        };
    }

    #endregion

    #region Common

    public async Task<TResult> HandleResponseAsync<TResult, TResponseType>(PayPalExpressCheckoutPaymentSettings settings,
        TResponseType response, TResult result,
        Action<TResult, TResponseType> onSuccess,
        Action<TResult, TResponseType> onFailure,
        Order order = null) where TResponseType : AbstractResponseType
    {
        if (response.Ack == AckCodeType.Success || response.Ack == AckCodeType.SuccessWithWarning)
            onSuccess(result, response);
        else
            onFailure(result, response);

        await LogResponseAsync(settings, response, order);

        return result;
    }

    public void AddErrors(IEnumerable<ErrorType> errors, Action<string> addError)
    {
        foreach (var errorType in errors)
        {
            var sb = new StringBuilder()
                .Append("LongMessage: ")
                .Append(errorType.LongMessage)
                .Append(Environment.NewLine)
                .Append("ShortMessage: ")
                .Append(errorType.ShortMessage)
                .Append(Environment.NewLine)
                .Append("ErrorCode: ")
                .Append(errorType.ErrorCode)
                .Append(Environment.NewLine);

            addError(sb.ToString());
        }
    }

    public CustomSecurityHeaderType GetCredentials(PayPalExpressCheckoutPaymentSettings settings)
    {
        return new()
        {
            Credentials = settings.DoNotHaveBusinessAccount
                ? new UserIdPasswordType { Subject = settings.EmailAddress }
                : new UserIdPasswordType { Signature = settings.ApiSignature, Username = settings.Username, Password = settings.Password }
        };
    }

    public PayPalAPIAAInterfaceClient GetAAClient(PayPalExpressCheckoutPaymentSettings settings)
    {
        return new(new BasicHttpsBinding(), new EndpointAddress(settings.IsLive ? "https://api-3t.paypal.com/2.0/" : "https://api-3t.sandbox.paypal.com/2.0/"));
    }

    public PayPalAPIInterfaceClient GetClient(PayPalExpressCheckoutPaymentSettings settings)
    {
        return new(new BasicHttpsBinding(), new EndpointAddress(settings.IsLive ? "https://api-3t.paypal.com/2.0/" : "https://api-3t.sandbox.paypal.com/2.0/"));
    }

    #endregion

    #region Checkout

    public async Task<IList<ShoppingCartItem>> GetCartAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        return await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
    }

    public async Task<bool> IsAllowedToCheckoutAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        return !await _customerService.IsGuestAsync(customer) || _orderSettings.AnonymousCheckoutAllowed;
    }

    public async Task<(string Url, string Error)> ProcessSubmitButtonAsync(IList<ShoppingCartItem> cart)
    {
        var credentials = GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = await GetSetExpressCheckoutRequestAsync(cart);
        var response = client.SetExpressCheckout(ref credentials, request);

        var redirectUrl = string.Empty;
        var error = string.Empty;

        await HandleResponseAsync(_payPalExpressCheckoutPaymentSettings, response, new ProcessPaymentResult(),
            onSuccess: (paymentResult, type) =>
            {
                redirectUrl = string.Format(_payPalExpressCheckoutPaymentSettings.IsLive
                    ? "https://www.paypal.com/webscr?cmd=_express-checkout&token={0}"
                    : "https://www.sandbox.paypal.com/webscr?cmd=_express-checkout&token={0}", response.Token);
            },
            onFailure: (paymentResult, type) =>
            {
                _logger.InsertLogAsync(LogLevel.Error, "Error passing cart to PayPal",
                    string.Join(", ", response.Errors.Select(errorType => errorType.ErrorCode + ": " + errorType.LongMessage))).Wait();
                error = "An error occurred setting up your cart for PayPal.";
                redirectUrl = _webHelper.GetUrlReferrer();
            });

        return (redirectUrl, error);
    }

    public async Task<bool> ProcessReturnAsync(string token)
    {
        var credentials = GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = GetGetExpressCheckoutDetailsRequest(token);
        var response = client.GetExpressCheckoutDetails(ref credentials, request);

        if (_payPalExpressCheckoutPaymentSettings.EnableDebugLogging)
        {
            var fullMessage = JsonConvert.SerializeObject(response);
            var chunks = fullMessage.ToList().Chunk(3500).ToList();
            for (var index = 0; index < chunks.Count; index++)
            {
                await _logger.InsertLogAsync(LogLevel.Debug, $"{response.GetType().Name} returned - Part {index + 1} of {chunks.Count}", new string(chunks[index]));
            }
        }

        if (response.Ack != AckCodeType.Success && response.Ack != AckCodeType.SuccessWithWarning)
            return false;

        var processPaymentRequest = await SetCheckoutDetailsAsync(response.GetExpressCheckoutDetailsResponseDetails);
        await _orderProcessingService.SetProcessPaymentRequestAsync(processPaymentRequest);

        var customer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId);
        await _workContext.SetCurrentCustomerAsync(customer);
        await _customerService.UpdateCustomerAsync(customer);

        return true;
    }

    public async Task<CheckoutPlaceOrderModel> PlaceOrderAsync()
    {
        var model = new CheckoutPlaceOrderModel();
        try
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();

            var processPaymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync();
            if (processPaymentRequest is null)
            {
                model.RedirectToCart = true;
                return model;
            }

            //prevent 2 orders being placed within an X seconds time frame
            if (!await IsMinimumOrderPlacementIntervalValidAsync())
                throw new Exception(await _localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval"));

            //place order
            processPaymentRequest.StoreId = store.Id;
            processPaymentRequest.CustomerId = customer.Id;
            processPaymentRequest.PaymentMethodSystemName = Defaults.SystemName;
            var placeOrderResult = await _orderProcessingService.PlaceOrderAsync(processPaymentRequest);

            if (placeOrderResult.Success)
            {
                var paymentResponse = await _httpContextAccessor.HttpContext.Session
                    .GetAsync<DoExpressCheckoutPaymentResponseType>(Defaults.CheckoutPaymentResponseTypeKey);

                if (paymentResponse != null)
                    await LogResponseAsync(_payPalExpressCheckoutPaymentSettings, paymentResponse, placeOrderResult.PlacedOrder);

                //clear payment request
                await _orderProcessingService.SetProcessPaymentRequestAsync(null);
                await _httpContextAccessor.HttpContext.Session.RemoveAsync(Defaults.CheckoutPaymentResponseTypeKey);

                var postProcessPaymentRequest = new PostProcessPaymentRequest
                {
                    Order = placeOrderResult.PlacedOrder
                };
                await _paymentService.PostProcessPaymentAsync(postProcessPaymentRequest);

                if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                {
                    //redirection or POST has been done in PostProcessPayment
                    model.IsRedirected = true;
                    return model;
                }

                model.CompletedId = placeOrderResult.PlacedOrder.Id;
                return model;
            }

            foreach (var error in placeOrderResult.Errors)
                model.Warnings.Add(error);
        }
        catch (Exception exc)
        {
            await _logger.WarningAsync(exc.Message, exc);
            model.Warnings.Add(exc.Message);
        }

        return model;
    }

    public async Task<CheckoutConfirmModel> PrepareConfirmOrderModelAsync(IList<ShoppingCartItem> cart)
    {
        var model = new CheckoutConfirmModel();
        var minOrderTotalAmountOk = await _orderProcessingService.ValidateMinOrderTotalAmountAsync(cart);
        if (minOrderTotalAmountOk)
            return model;

        var currency = await _workContext.GetWorkingCurrencyAsync();
        var minOrderTotalAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(_orderSettings.MinOrderTotalAmount, currency);
        var value = await _priceFormatter.FormatPriceAsync(minOrderTotalAmount, true, false);
        model.MinOrderTotalWarning = string.Format(await _localizationService.GetResourceAsync("Checkout.MinOrderTotalAmount"), value);

        return model;
    }

    #endregion

    #region Shipping

    public async Task<CheckoutShippingMethodModel> PrepareShippingMethodModelAsync(IList<ShoppingCartItem> cart)
    {
        var model = new CheckoutShippingMethodModel();

        var customer = await _workContext.GetCurrentCustomerAsync();
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var shippingAddress = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId ?? 0);
        var getShippingOptionResponse = await _shippingService.GetShippingOptionsAsync(cart, shippingAddress);

        if (getShippingOptionResponse.Success)
        {
            //performance optimization. cache returned shipping options.
            //we'll use them later (after a customer has selected an option).
            await _genericAttributeService
                .SaveAttributeAsync(customer, NopCustomerDefaults.OfferedShippingOptionsAttribute, getShippingOptionResponse.ShippingOptions, store.Id);

            foreach (var shippingOption in getShippingOptionResponse.ShippingOptions)
            {
                var soModel = new CheckoutShippingMethodModel.ShippingMethodModel
                {
                    Name = shippingOption.Name,
                    Description = shippingOption.Description,
                    ShippingRateComputationMethodSystemName = shippingOption.ShippingRateComputationMethodSystemName
                };

                //adjust rate
                var (shippingTotal, _) = await _orderTotalCalculationService.AdjustShippingRateAsync(shippingOption.Rate, cart);
                var (_, rateBase) = await _taxService.GetShippingPriceAsync(shippingTotal, customer);
                var rate = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(rateBase, currency);
                soModel.Fee = await _priceFormatter.FormatShippingPriceAsync(rate, true);

                model.ShippingMethods.Add(soModel);
            }

            //find a selected (previously) shipping method
            var selectedShippingOption = await _genericAttributeService
                .GetAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);

            CheckoutShippingMethodModel.ShippingMethodModel shippingOptionToSelect;

            if (selectedShippingOption != null)
            {
                shippingOptionToSelect = model.ShippingMethods.ToList().Find(so => !string.IsNullOrEmpty(so.Name)
                    && so.Name.Equals(selectedShippingOption.Name, StringComparison.InvariantCultureIgnoreCase)
                    && !string.IsNullOrEmpty(so.ShippingRateComputationMethodSystemName)
                    && so.ShippingRateComputationMethodSystemName.Equals(selectedShippingOption.ShippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase));
                if (shippingOptionToSelect != null)
                    shippingOptionToSelect.Selected = true;
            }

            //if no option has been selected, let's do it for the first one
            if (model.ShippingMethods.FirstOrDefault(so => so.Selected) != null)
                return model;

            shippingOptionToSelect = model.ShippingMethods.FirstOrDefault();
            if (shippingOptionToSelect != null)
                shippingOptionToSelect.Selected = true;
        }
        else
        {
            foreach (var error in getShippingOptionResponse.Errors)
                model.Warnings.Add(error);
        }

        return model;
    }

    public async Task<bool> SetShippingMethodAsync(IList<ShoppingCartItem> cart, string shippingoption)
    {
        //parse selected method 
        if (string.IsNullOrEmpty(shippingoption))
            return false;

        var splittedOption = shippingoption.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
        if (splittedOption.Length != 2)
            return false;

        var selectedName = splittedOption[0];
        var shippingRateComputationMethodSystemName = splittedOption[1];

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        //find it
        //performance optimization. try cache first
        var shippingOptions = await _genericAttributeService.GetAttributeAsync<List<ShippingOption>>(customer, NopCustomerDefaults.OfferedShippingOptionsAttribute, store.Id);
        if (shippingOptions == null || shippingOptions.Count == 0)
        {
            var shippingAddress = await _addressService.GetAddressByIdAsync(customer.ShippingAddressId ?? 0);

            //not found? let's load them using shipping service
            shippingOptions = (await _shippingService.GetShippingOptionsAsync(cart, shippingAddress, allowedShippingRateComputationMethodSystemName: shippingRateComputationMethodSystemName))
                .ShippingOptions
                .ToList();
        }
        else
        {
            //loaded cached results. let's filter result by a chosen shipping rate computation method
            shippingOptions = shippingOptions
                .Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
        }

        var shippingOption = shippingOptions
            .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
        if (shippingOption == null)
            return false;

        //save
        await _genericAttributeService
            .SaveAttributeAsync(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, shippingOption, store.Id);

        return true;
    }

    public async Task SetShippingMethodToNullAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        await _genericAttributeService
            .SaveAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, store.Id);
    }

    #endregion

    #region Requests

    public async Task<DoExpressCheckoutPaymentReq> GetDoExpressCheckoutPaymentRequestAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var currency = await _workContext.GetWorkingCurrencyAsync();
        var currencyCodeType = GetCurrencyCodeType(currency.CurrencyCode);

        return new()
        {
            DoExpressCheckoutPaymentRequest = new()
            {
                Version = GetVersion(),
                DoExpressCheckoutPaymentRequestDetails = new()
                {
                    Token = processPaymentRequest.CustomValues[Defaults.PaypalTokenKey],
                    PayerID = processPaymentRequest.CustomValues[Defaults.PaypalPayerIdKey],
                    PaymentAction = GetPaymentAction(_payPalExpressCheckoutPaymentSettings),
                    PaymentActionSpecified = true,
                    ButtonSource = Defaults.PartnerCode,
                    PaymentDetails = new[]
                    {
                        new PaymentDetailsType
                        {
                            OrderTotal = await GetBasicAmountTypeAsync(processPaymentRequest.OrderTotal, currencyCodeType),
                            Custom = processPaymentRequest.OrderGuid.ToString(),
                            ButtonSource = Defaults.PartnerCode,
                            InvoiceID = processPaymentRequest.OrderGuid.ToString(),
                            NotifyURL = _nopUrlHelper.RouteUrl(Defaults.Route.Ipn, null, _webHelper.GetCurrentRequestProtocol())
                        }
                    }
                }
            }
        };
    }

    public async Task<DoCaptureReq> GetDoCaptureRequestAsync(CapturePaymentRequest capturePaymentRequest)
    {
        var currencyCodeType = GetCurrencyCodeType(capturePaymentRequest.Order.CustomerCurrencyCode);
        return new()
        {
            DoCaptureRequest = new()
            {
                Amount = await GetBasicAmountTypeAsync(capturePaymentRequest.Order.OrderTotal, currencyCodeType),
                AuthorizationID = capturePaymentRequest.Order.AuthorizationTransactionId,
                CompleteType = CompleteCodeType.Complete,
                InvoiceID = capturePaymentRequest.Order.OrderGuid.ToString(),
                Version = GetVersion(),
                MsgSubID = capturePaymentRequest.Order.Id + "-capture"
            }
        };
    }

    public async Task<RefundTransactionReq> GetRefundTransactionRequestAsync(RefundPaymentRequest refundPaymentRequest)
    {
        var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId)
            ?? throw new NopException("Primary store currency cannot be loaded");

        var transactionId = refundPaymentRequest.Order.CaptureTransactionId;
        var refundType = refundPaymentRequest.IsPartialRefund ? RefundType.Partial : RefundType.Full;
        var currencyCodeType = GetCurrencyCodeType(currency.CurrencyCode);

        return new()
        {
            RefundTransactionRequest = new()
            {
                RefundType = refundType,
                RefundTypeSpecified = true,
                Version = GetVersion(),
                TransactionID = transactionId,
                Amount = await GetBasicAmountTypeAsync(refundPaymentRequest.AmountToRefund, currencyCodeType),
                MsgSubID = $"{refundPaymentRequest.Order.Id}-{refundPaymentRequest.IsPartialRefund}-{refundPaymentRequest.AmountToRefund:0.00}"
            }
        };
    }

    public DoVoidReq GetVoidRequest(VoidPaymentRequest voidPaymentRequest)
    {
        return new()
        {
            DoVoidRequest = new()
            {
                Version = GetVersion(),
                AuthorizationID = voidPaymentRequest.Order.AuthorizationTransactionId,
                MsgSubID = voidPaymentRequest.Order.Id + "-void"
            }
        };
    }

    public async Task<CreateRecurringPaymentsProfileReq> GetCreateRecurringPaymentsProfileRequestAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return new()
        {
            CreateRecurringPaymentsProfileRequest = new()
            {
                Version = GetVersion(),
                CreateRecurringPaymentsProfileRequestDetails = await GetCreateRecurringPaymentProfileRequestDetailsAsync(processPaymentRequest)
            }
        };
    }

    public ManageRecurringPaymentsProfileStatusReq GetCancelRecurringPaymentRequest(CancelRecurringPaymentRequest cancelRecurringPaymentRequest)
    {
        return new()
        {
            ManageRecurringPaymentsProfileStatusRequest = new()
            {
                Version = GetVersion(),
                ManageRecurringPaymentsProfileStatusRequestDetails = new()
                {
                    Action = StatusChangeActionType.Cancel,
                    ProfileID = cancelRecurringPaymentRequest.Order.OrderGuid.ToString()
                }
            }
        };
    }

    #endregion

    #endregion
}