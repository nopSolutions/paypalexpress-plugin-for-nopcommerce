using System;
using System.Linq;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutShippingAddressService : IPayPalExpressCheckoutShippingAddressService
    {
        private readonly IWorkContext _workContext;
        private readonly AddressSettings _addressSettings;
        private readonly ILocalizationService _localizationService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;

        public PayPalExpressCheckoutShippingAddressService(IWorkContext workContext, 
                                                    AddressSettings addressSettings, ILocalizationService localizationService,
                                                    IStateProvinceService stateProvinceService, ICountryService countryService, 
                                                    ICustomerService customerService)
        {
            _workContext = workContext;
            _addressSettings = addressSettings;
            _localizationService = localizationService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
            _customerService = customerService;
        }

        public CheckoutShippingAddressModel PrepareShippingAddressModel(int? selectedCountryId = null)
        {
            var model = new CheckoutShippingAddressModel();
            //existing addresses
            var addresses = _workContext.CurrentCustomer.Addresses.Where(a => a.Country == null || a.Country.AllowsShipping).ToList();
            foreach (var address in addresses)
            {
                var addressModel = new AddressModel();
                addressModel.PrepareModel(address,
                                          false,
                                          _addressSettings);
                model.ExistingAddresses.Add(addressModel);
            }

            //new address
            model.NewAddress.CountryId = selectedCountryId;
            model.NewAddress.PrepareModel(null,
                                          false,
                                          _addressSettings,
                                          _localizationService,
                                          _stateProvinceService,
                                          () => _countryService.GetAllCountriesForShipping());
            return model;
        }

        public bool SetExistingAddress(int addressId)
        {
            var address = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address == null)
                return false;

            _workContext.CurrentCustomer.ShippingAddress = address;
            _customerService.UpdateCustomer(_workContext.CurrentCustomer);
            return true;
        }

        public bool SetNewAddress(CheckoutShippingAddressModel checkoutShippingAddressModel)
        {
            var address = checkoutShippingAddressModel.NewAddress.ToEntity();
            address.CreatedOnUtc = DateTime.UtcNow;
            //some validation
            if (address.CountryId == 0)
                address.CountryId = null;
            if (address.StateProvinceId == 0)
                address.StateProvinceId = null;
            _workContext.CurrentCustomer.Addresses.Add(address);
            _workContext.CurrentCustomer.ShippingAddress = address;
            _customerService.UpdateCustomer(_workContext.CurrentCustomer);
            return true;
        }
    }
}