using System;
using System.Linq;
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
    public class PayPalCheckoutDetailsService : IPayPalCheckoutDetailsService
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

        public ProcessPaymentRequest SetCheckoutDetails(GetExpressCheckoutDetailsResponseDetailsType checkoutDetails)
        {
            // get customer & cart
            int customerId = Convert.ToInt32(_workContext.CurrentCustomer.Id.ToString());
            var customer = _customerService.GetCustomerById(customerId);

            _workContext.CurrentCustomer = customer;

            var cart = customer.ShoppingCartItems.Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart).ToList();

            // get/update billing address
            string billingFirstName = checkoutDetails.PayerInfo.PayerName.FirstName;
            string billingLastName = checkoutDetails.PayerInfo.PayerName.LastName;
            string billingEmail = checkoutDetails.PayerInfo.Payer;
            string billingAddress1 = checkoutDetails.PayerInfo.Address.Street1;
            string billingAddress2 = checkoutDetails.PayerInfo.Address.Street2;
            string billingPhoneNumber = checkoutDetails.PayerInfo.ContactPhone;
            string billingCity = checkoutDetails.PayerInfo.Address.CityName;
            int? billingStateProvinceId = null;
            var billingStateProvince = _stateProvinceService.GetStateProvinceByAbbreviation(checkoutDetails.PayerInfo.Address.StateOrProvince);
            if (billingStateProvince != null)
                billingStateProvinceId = billingStateProvince.Id;
            string billingZipPostalCode = checkoutDetails.PayerInfo.Address.PostalCode;
            int? billingCountryId = null;
            var billingCountry = _countryService.GetCountryByTwoLetterIsoCode(checkoutDetails.PayerInfo.Address.Country.ToString());
            if (billingCountry != null)
                billingCountryId = billingCountry.Id;

            var billingAddress = _addressService.FindAddress(_workContext.CurrentCustomer.Addresses.ToList(),
                billingFirstName, billingLastName, billingPhoneNumber,
                billingEmail, string.Empty, string.Empty,
                billingAddress1, billingAddress2, billingCity,
                billingCountry?.Name, billingStateProvinceId, billingZipPostalCode,
                billingCountryId, null);    //TODO process custom attributes

            if (billingAddress == null)
            {
                billingAddress = new Core.Domain.Common.Address()
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
                                         CreatedOnUtc = DateTime.UtcNow,
                                     };
                customer.Addresses.Add(billingAddress);
            }

            //set default billing address
            customer.BillingAddress = billingAddress;
            _customerService.UpdateCustomer(customer);

            _genericAttributeService.SaveAttribute<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, customer.RegisteredInStoreId);

            bool shoppingCartRequiresShipping = _shoppingCartService.ShoppingCartRequiresShipping(cart);
            if (shoppingCartRequiresShipping)
            {
                var paymentDetails = checkoutDetails.PaymentDetails.FirstOrDefault();
                string[] shippingFullname = paymentDetails.ShipToAddress.Name.Trim()
                                                          .Split(new char[] { ' ' }, 2,
                                                                 StringSplitOptions.RemoveEmptyEntries);
                string shippingFirstName = shippingFullname[0];
                string shippingLastName = string.Empty;
                if (shippingFullname.Length > 1)
                    shippingLastName = shippingFullname[1];
                string shippingEmail = checkoutDetails.PayerInfo.Payer;
                string shippingAddress1 = paymentDetails.ShipToAddress.Street1;
                string shippingAddress2 = paymentDetails.ShipToAddress.Street2;
                string shippingPhoneNumber = paymentDetails.ShipToAddress.Phone;
                string shippingCity = paymentDetails.ShipToAddress.CityName;
                int? shippingStateProvinceId = null;
                var shippingStateProvince =
                    _stateProvinceService.GetStateProvinceByAbbreviation(
                        paymentDetails.ShipToAddress.StateOrProvince);
                if (shippingStateProvince != null)
                    shippingStateProvinceId = shippingStateProvince.Id;
                int? shippingCountryId = null;
                string shippingZipPostalCode = paymentDetails.ShipToAddress.PostalCode;
                var shippingCountry =
                    _countryService.GetCountryByTwoLetterIsoCode(paymentDetails.ShipToAddress.Country.ToString());
                if (shippingCountry != null)
                    shippingCountryId = shippingCountry.Id;

                var shippingAddress = _addressService.FindAddress(_workContext.CurrentCustomer.Addresses.ToList(),
                    shippingFirstName, shippingLastName, shippingPhoneNumber,
                    shippingEmail, string.Empty, string.Empty,
                    shippingAddress1, shippingAddress2, shippingCity,
                    shippingCountry?.Name, shippingStateProvinceId, shippingZipPostalCode,
                    shippingCountryId, null);    //TODO process custom attributes


                if (shippingAddress == null)
                {
                    shippingAddress = new Core.Domain.Common.Address()
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
                                              CreatedOnUtc = DateTime.UtcNow,
                                          };
                    customer.Addresses.Add(shippingAddress);
                }

                //set default shipping address
                customer.ShippingAddress = shippingAddress;
                _customerService.UpdateCustomer(customer);
            }

            var processPaymentRequest = new ProcessPaymentRequest { CustomerId = customerId };
            processPaymentRequest.CustomValues["PaypalToken"] = checkoutDetails.Token;
            processPaymentRequest.CustomValues["PaypalPayerId"] = checkoutDetails.PayerInfo.PayerID;
            return processPaymentRequest;
        }

    }
}