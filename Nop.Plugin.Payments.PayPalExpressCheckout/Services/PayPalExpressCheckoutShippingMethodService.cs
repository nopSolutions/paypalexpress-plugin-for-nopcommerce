using System;
using System.Collections.Generic;
using System.Linq;
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

        public CheckoutShippingMethodModel PrepareShippingMethodModel(IList<ShoppingCartItem> cart)
        {
            var model = new CheckoutShippingMethodModel();

            var shippingAddress = _addressService.GetAddressById(_workContext.CurrentCustomer.ShippingAddressId ?? 0);

            var getShippingOptionResponse = _shippingService.GetShippingOptions(cart, shippingAddress);

            if (getShippingOptionResponse.Success)
            {
                //performance optimization. cache returned shipping options.
                //we'll use them later (after a customer has selected an option).
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    NopCustomerDefaults.OfferedShippingOptionsAttribute,
                    getShippingOptionResponse.ShippingOptions,
                    _storeContext.CurrentStore.Id);

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
                    var shippingTotal = _orderTotalCalculationService.AdjustShippingRate(
                        shippingOption.Rate, cart, out _);

                    var rateBase = _taxService.GetShippingPrice(shippingTotal, _workContext.CurrentCustomer);
                    var rate =
                        _currencyService.ConvertFromPrimaryStoreCurrency(rateBase, _workContext.WorkingCurrency);
                    soModel.Fee = _priceFormatter.FormatShippingPrice(rate, true);

                    model.ShippingMethods.Add(soModel);
                }

                //find a selected (previously) shipping method
                var selectedShippingOption = _genericAttributeService.GetAttribute<ShippingOption>(_workContext.CurrentCustomer, NopCustomerDefaults.SelectedShippingOptionAttribute, _storeContext.CurrentStore.Id);

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

        public bool SetShippingMethod(IList<ShoppingCartItem> cart, string shippingoption)
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
            var shippingOptions = _genericAttributeService.GetAttribute<List<ShippingOption>>(_workContext.CurrentCustomer, NopCustomerDefaults.OfferedShippingOptionsAttribute, _storeContext.CurrentStore.Id);
            if (shippingOptions == null || shippingOptions.Count == 0)
            {
                var shippingAddress = _addressService.GetAddressById(_workContext.CurrentCustomer.ShippingAddressId ?? 0);

                //not found? let's load them using shipping service
                shippingOptions = _shippingService
                    .GetShippingOptions(cart, shippingAddress,
                    allowedShippingRateComputationMethodSystemName: shippingRateComputationMethodSystemName)
                    .ShippingOptions
                    .ToList();
            }
            else
            {
                //loaded cached results. let's filter result by a chosen shipping rate computation method
                shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                                                 .ToList();
            }

            var shippingOption = shippingOptions
                .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
            if (shippingOption == null)
                return false;

            //save
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                NopCustomerDefaults.SelectedShippingOptionAttribute, shippingOption, _storeContext.CurrentStore.Id);

            return true;
        }

        public void SetShippingMethodToNull()
        {
            _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer,
                NopCustomerDefaults.SelectedShippingOptionAttribute, null, _storeContext.CurrentStore.Id);
        }
    }
}