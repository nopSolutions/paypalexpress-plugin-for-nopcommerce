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
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICountryService _countryService;
        private readonly IGenericAttributeService _genericAttributeService;

        public PayPalCheckoutDetailsService(IWorkContext workContext, ICustomerService customerService, IStateProvinceService stateProvinceService, ICountryService countryService, IGenericAttributeService genericAttributeService)
        {
            _workContext = workContext;
            _customerService = customerService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
            _genericAttributeService = genericAttributeService;
        }

        public ProcessPaymentRequest SetCheckoutDetails(GetExpressCheckoutDetailsResponseDetailsType checkoutDetails)
        {
            // get customer & cart
            //var customer = customer;
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

            var billingAddress = customer.Addresses.ToList().FindAddress(
                billingFirstName, billingLastName, billingPhoneNumber,
                billingEmail, string.Empty, string.Empty, billingAddress1, billingAddress2, billingCity,
                billingStateProvinceId, billingZipPostalCode, billingCountryId,
                //TODO process custom attributes
                null);

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

            _genericAttributeService.SaveAttribute<ShippingOption>(customer, SystemCustomerAttributeNames.SelectedShippingOption, null);

            bool shoppingCartRequiresShipping = cart.RequiresShipping();
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

                var shippingAddress = customer.Addresses.ToList().FindAddress(
                    shippingFirstName, shippingLastName, shippingPhoneNumber,
                    shippingEmail, string.Empty, string.Empty,
                    shippingAddress1, shippingAddress2, shippingCity,
                    shippingStateProvinceId, shippingZipPostalCode, shippingCountryId,
                    //TODO process custom attributes
                null);

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