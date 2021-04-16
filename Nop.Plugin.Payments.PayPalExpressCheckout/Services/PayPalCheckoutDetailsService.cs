using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalCheckoutDetailsService
    {
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IWorkContext _workContext;

        public PayPalCheckoutDetailsService(IAddressService addressService,
            ICountryService countryService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IShoppingCartService shoppingCartService,
            IStateProvinceService stateProvinceService,
            IWorkContext workContext)
        {
            _addressService = addressService;
            _countryService = countryService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _shoppingCartService = shoppingCartService;
            _stateProvinceService = stateProvinceService;
            _workContext = workContext;
        }

        public async Task<ProcessPaymentRequest> SetCheckoutDetailsAsync(GetExpressCheckoutDetailsResponseDetailsType checkoutDetails)
        {
            // get customer & cart
            var customerId = Convert.ToInt32((await _workContext.GetCurrentCustomerAsync()).Id.ToString());
            var customer = await _customerService.GetCustomerByIdAsync(customerId);

            await _workContext.SetCurrentCustomerAsync(customer);

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart);

            // get/update billing address
            var billingFirstName = checkoutDetails.PayerInfo.PayerName.FirstName;
            var billingLastName = checkoutDetails.PayerInfo.PayerName.LastName;
            var billingEmail = checkoutDetails.PayerInfo.Payer;
            var billingAddress1 = checkoutDetails.PayerInfo.Address.Street1;
            var billingAddress2 = checkoutDetails.PayerInfo.Address.Street2;
            var billingPhoneNumber = checkoutDetails.PayerInfo.ContactPhone;
            var billingCity = checkoutDetails.PayerInfo.Address.CityName;
            int? billingStateProvinceId = null;
            var billingStateProvince = await _stateProvinceService.GetStateProvinceByAbbreviationAsync(checkoutDetails.PayerInfo.Address.StateOrProvince);
            if (billingStateProvince != null)
                billingStateProvinceId = billingStateProvince.Id;
            var billingZipPostalCode = checkoutDetails.PayerInfo.Address.PostalCode;
            int? billingCountryId = null;
            var billingCountry = await _countryService.GetCountryByTwoLetterIsoCodeAsync(checkoutDetails.PayerInfo.Address.Country.ToString());
            if (billingCountry != null)
                billingCountryId = billingCountry.Id;

            var billingAddress = _addressService.FindAddress((await _customerService.GetAddressesByCustomerIdAsync((await _workContext.GetCurrentCustomerAsync()).Id)).ToList(),
                billingFirstName, billingLastName, billingPhoneNumber,
                billingEmail, string.Empty, string.Empty,
                billingAddress1, billingAddress2, billingCity,
                billingCountry?.Name, billingStateProvinceId, billingZipPostalCode,
                billingCountryId, null); //TODO process custom attributes

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

            await _genericAttributeService.SaveAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, customer.RegisteredInStoreId);

            var shoppingCartRequiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
            if (shoppingCartRequiresShipping)
            {
                var paymentDetails = checkoutDetails.PaymentDetails.FirstOrDefault() ?? new PaymentDetailsType();
                var shippingFullname = paymentDetails.ShipToAddress.Name.Trim()
                    .Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                var shippingFirstName = shippingFullname[0];
                var shippingLastName = string.Empty;
                if (shippingFullname.Length > 1)
                    shippingLastName = shippingFullname[1];
                var shippingEmail = checkoutDetails.PayerInfo.Payer;
                var shippingAddress1 = paymentDetails.ShipToAddress.Street1;
                var shippingAddress2 = paymentDetails.ShipToAddress.Street2;
                var shippingPhoneNumber = paymentDetails.ShipToAddress.Phone;
                var shippingCity = paymentDetails.ShipToAddress.CityName;
                int? shippingStateProvinceId = null;
                var shippingStateProvince = await _stateProvinceService.GetStateProvinceByAbbreviationAsync(paymentDetails.ShipToAddress.StateOrProvince);
                if (shippingStateProvince != null)
                    shippingStateProvinceId = shippingStateProvince.Id;
                int? shippingCountryId = null;
                var shippingZipPostalCode = paymentDetails.ShipToAddress.PostalCode;
                var shippingCountry =
                    await _countryService.GetCountryByTwoLetterIsoCodeAsync(paymentDetails.ShipToAddress.Country.ToString());
                if (shippingCountry != null)
                    shippingCountryId = shippingCountry.Id;

                var shippingAddress = _addressService.FindAddress((await _customerService.GetAddressesByCustomerIdAsync((await _workContext.GetCurrentCustomerAsync()).Id)).ToList(),
                    shippingFirstName, shippingLastName, shippingPhoneNumber,
                    shippingEmail, string.Empty, string.Empty,
                    shippingAddress1, shippingAddress2, shippingCity,
                    shippingCountry?.Name, shippingStateProvinceId, shippingZipPostalCode,
                    shippingCountryId, null); //TODO process custom attributes

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
                CustomerId = customerId,
                CustomValues =
                {
                    [Defaults.PaypalTokenKey] = checkoutDetails.Token,
                    [Defaults.PaypalPayerIdKey] = checkoutDetails.PayerInfo.PayerID
                }
            };

            return processPaymentRequest;
        }
    }
}