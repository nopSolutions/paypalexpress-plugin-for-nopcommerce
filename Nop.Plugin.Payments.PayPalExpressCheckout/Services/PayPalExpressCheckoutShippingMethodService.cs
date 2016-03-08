using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
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
    public class PayPalExpressCheckoutShippingMethodService : IPayPalExpressCheckoutShippingMethodService
    {
        private readonly IShippingService _shippingService;
        private readonly IWorkContext _workContext;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IStoreContext _storeContext;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ITaxService _taxService;
        private readonly ICurrencyService _currencyService;
        private readonly IPriceFormatter _priceFormatter;

        public PayPalExpressCheckoutShippingMethodService(
            IShippingService shippingService,
            IWorkContext workContext,
            IGenericAttributeService genericAttributeService,
            IStoreContext storeContext,
            IOrderTotalCalculationService orderTotalCalculationService,
            ITaxService taxService,
            ICurrencyService currencyService,
            IPriceFormatter priceFormatter
            )
        {
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

            var getShippingOptionResponse = _shippingService.GetShippingOptions(cart, _workContext.CurrentCustomer.ShippingAddress);
            if (getShippingOptionResponse.Success)
            {
                //performance optimization. cache returned shipping options.
                //we'll use them later (after a customer has selected an option).
                _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                                                       SystemCustomerAttributeNames.OfferedShippingOptions,
                                                       getShippingOptionResponse.ShippingOptions,
                                                       _storeContext.CurrentStore.Id);

                foreach (var shippingOption in getShippingOptionResponse.ShippingOptions)
                {
                    var soModel = new CheckoutShippingMethodModel.ShippingMethodModel()
                                      {
                                          Name = shippingOption.Name,
                                          Description = shippingOption.Description,
                                          ShippingRateComputationMethodSystemName = shippingOption.ShippingRateComputationMethodSystemName,
                                      };

                    //adjust rate
                    Discount appliedDiscount = null;
                    var shippingTotal = _orderTotalCalculationService.AdjustShippingRate(
                        shippingOption.Rate, cart, out appliedDiscount);

                    decimal rateBase = _taxService.GetShippingPrice(shippingTotal, _workContext.CurrentCustomer);
                    decimal rate = _currencyService.ConvertFromPrimaryStoreCurrency(rateBase, _workContext.WorkingCurrency);
                    soModel.Fee = _priceFormatter.FormatShippingPrice(rate, true);

                    model.ShippingMethods.Add(soModel);
                }

                //find a selected (previously) shipping method
                var selectedShippingOption = _workContext.CurrentCustomer.GetAttribute<ShippingOption>(SystemCustomerAttributeNames.SelectedShippingOption, _storeContext.CurrentStore.Id);
                if (selectedShippingOption != null)
                {
                    var shippingOptionToSelect = model.ShippingMethods.ToList()
                                                      .Find(so => !String.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedShippingOption.Name, StringComparison.InvariantCultureIgnoreCase) &&
                                                                  !String.IsNullOrEmpty(so.ShippingRateComputationMethodSystemName) && so.ShippingRateComputationMethodSystemName.Equals(selectedShippingOption.ShippingRateComputationMethodSystemName, StringComparison.InvariantCultureIgnoreCase));
                    if (shippingOptionToSelect != null)
                        shippingOptionToSelect.Selected = true;
                }
                //if no option has been selected, let's do it for the first one
                if (model.ShippingMethods.FirstOrDefault(so => so.Selected) == null)
                {
                    var shippingOptionToSelect = model.ShippingMethods.FirstOrDefault();
                    if (shippingOptionToSelect != null)
                        shippingOptionToSelect.Selected = true;
                }
            }
            else
                foreach (var error in getShippingOptionResponse.Errors)
                    model.Warnings.Add(error);

            return model;
        }

        public bool SetShippingMethod(IList<ShoppingCartItem> cart, string shippingoption)
        {
            //parse selected method 
            if (String.IsNullOrEmpty(shippingoption))
                return false;
            var splittedOption = shippingoption.Split(new string[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
            if (splittedOption.Length != 2)
                return false;
            string selectedName = splittedOption[0];
            string shippingRateComputationMethodSystemName = splittedOption[1];

            //find it
            //performance optimization. try cache first
            var shippingOptions = _workContext.CurrentCustomer.GetAttribute<List<ShippingOption>>(SystemCustomerAttributeNames.OfferedShippingOptions, _storeContext.CurrentStore.Id);
            if (shippingOptions == null || shippingOptions.Count == 0)
            {
                //not found? let's load them using shipping service
                shippingOptions = _shippingService
                    .GetShippingOptions(cart, _workContext.CurrentCustomer.ShippingAddress, shippingRateComputationMethodSystemName)
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
                .Find(so => !String.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.InvariantCultureIgnoreCase));
            if (shippingOption == null)
                return false;

            //save
            _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer, SystemCustomerAttributeNames.SelectedShippingOption, shippingOption, _storeContext.CurrentStore.Id);
            
            return true;
        }

        public void SetShippingMethodToNull()
        {
            _genericAttributeService.SaveAttribute<ShippingOption>(_workContext.CurrentCustomer,
                SystemCustomerAttributeNames.SelectedShippingOption, null, _storeContext.CurrentStore.Id);
        }
    }
}