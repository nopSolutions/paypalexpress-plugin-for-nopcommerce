using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Shipping;
using Nop.Services.Tax;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutShippingMethodService
    {
        private readonly IAddressService _addressService;
        private readonly IShippingService _shippingService;
        private readonly IWorkContext _workContext;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IStoreContext _storeContext;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ITaxService _taxService;
        private readonly ICurrencyService _currencyService;
        private readonly IPriceFormatter _priceFormatter;

        public PayPalExpressCheckoutShippingMethodService(
            IAddressService addressService,
            IShippingService shippingService,
            IWorkContext workContext,
            IGenericAttributeService genericAttributeService,
            IStoreContext storeContext,
            IOrderTotalCalculationService orderTotalCalculationService,
            ITaxService taxService,
            ICurrencyService currencyService,
            IPriceFormatter priceFormatter)
        {
            _addressService = addressService;
            _shippingService = shippingService;
            _workContext = workContext;
            _genericAttributeService = genericAttributeService;
            _storeContext = storeContext;
            _orderTotalCalculationService = orderTotalCalculationService;
            _taxService = taxService;
            _currencyService = currencyService;
            _priceFormatter = priceFormatter;
        }

        public async Task<CheckoutShippingMethodModel> PrepareShippingMethodModelAsync(IList<ShoppingCartItem> cart)
        {
            var model = new CheckoutShippingMethodModel();

            var shippingAddress = await _addressService.GetAddressByIdAsync((await _workContext.GetCurrentCustomerAsync()).ShippingAddressId ?? 0);

            var getShippingOptionResponse = await _shippingService.GetShippingOptionsAsync(cart, shippingAddress);

            if (getShippingOptionResponse.Success)
            {
                //performance optimization. cache returned shipping options.
                //we'll use them later (after a customer has selected an option).
                await _genericAttributeService.SaveAttributeAsync(await _workContext.GetCurrentCustomerAsync(),
                    NopCustomerDefaults.OfferedShippingOptionsAttribute,
                    getShippingOptionResponse.ShippingOptions,
                    (_storeContext.GetCurrentStoreAsync()).Id);

                foreach (var shippingOption in getShippingOptionResponse.ShippingOptions)
                {
                    var soModel = new CheckoutShippingMethodModel.ShippingMethodModel
                    {
                        Name = shippingOption.Name,
                        Description = shippingOption.Description,
                        ShippingRateComputationMethodSystemName =
                            shippingOption.ShippingRateComputationMethodSystemName
                    };

                    //adjust rate
                    var (shippingTotal, _) = await _orderTotalCalculationService.AdjustShippingRateAsync(
                        shippingOption.Rate, cart);

                    var (_, rateBase) = await _taxService.GetShippingPriceAsync(shippingTotal, await _workContext.GetCurrentCustomerAsync());
                    var rate =
                        await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(rateBase, await _workContext.GetWorkingCurrencyAsync());
                    soModel.Fee = await _priceFormatter.FormatShippingPriceAsync(rate, true);

                    model.ShippingMethods.Add(soModel);
                }

                //find a selected (previously) shipping method
                var selectedShippingOption = await _genericAttributeService.GetAttributeAsync<ShippingOption>(await _workContext.GetCurrentCustomerAsync(), NopCustomerDefaults.SelectedShippingOptionAttribute, (_storeContext.GetCurrentStoreAsync()).Id);

                CheckoutShippingMethodModel.ShippingMethodModel shippingOptionToSelect;

                if (selectedShippingOption != null)
                {
                    shippingOptionToSelect = model.ShippingMethods.ToList()
                                                      .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedShippingOption.Name, StringComparison.InvariantCultureIgnoreCase) &&
                                                                  !string.IsNullOrEmpty(so.ShippingRateComputationMethodSystemName) && so.ShippingRateComputationMethodSystemName.Equals(selectedShippingOption.ShippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase));
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

            //find it
            //performance optimization. try cache first
            var shippingOptions = await _genericAttributeService.GetAttributeAsync<List<ShippingOption>>(await _workContext.GetCurrentCustomerAsync(), NopCustomerDefaults.OfferedShippingOptionsAttribute, (await _storeContext.GetCurrentStoreAsync()).Id);
            if (shippingOptions == null || shippingOptions.Count == 0)
            {
                var shippingAddress = await _addressService.GetAddressByIdAsync((await _workContext.GetCurrentCustomerAsync()).ShippingAddressId ?? 0);

                //not found? let's load them using shipping service
                shippingOptions = (await _shippingService
                    .GetShippingOptionsAsync(cart, shippingAddress,
                    allowedShippingRateComputationMethodSystemName: shippingRateComputationMethodSystemName))
                    .ShippingOptions
                    .ToList();
            }
            else
                //loaded cached results. let's filter result by a chosen shipping rate computation method
                shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

            var shippingOption = shippingOptions
                .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
            if (shippingOption == null)
                return false;

            //save
            await _genericAttributeService.SaveAttributeAsync(await _workContext.GetCurrentCustomerAsync(),
                NopCustomerDefaults.SelectedShippingOptionAttribute, shippingOption, (await _storeContext.GetCurrentStoreAsync()).Id);

            return true;
        }

        public async Task SetShippingMethodToNullAsync()
        {
            await _genericAttributeService.SaveAttributeAsync<ShippingOption>(await _workContext.GetCurrentCustomerAsync(),
                NopCustomerDefaults.SelectedShippingOptionAttribute, null, (await _storeContext.GetCurrentStoreAsync()).Id);
        }
    }
}