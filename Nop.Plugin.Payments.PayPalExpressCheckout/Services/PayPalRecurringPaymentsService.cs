using System;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Payments;
using Nop.Services.Stores;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalRecurringPaymentsService
    {
        private readonly IAddressService _addressService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;
        private readonly PayPalCurrencyCodeParser _payPalCurrencyCodeParser;

        public PayPalRecurringPaymentsService(IAddressService addressService,
            ICountryService countryService,
            ICustomerService customerService,
            IStoreService storeService,
            IStateProvinceService stateProvinceService,
            IWorkContext workContext,
            PayPalCurrencyCodeParser payPalCurrencyCodeParser)
        {
            _addressService = addressService;
            _countryService = countryService;
            _customerService = customerService;
            _stateProvinceService = stateProvinceService;
            _storeService = storeService;
            _workContext = workContext;
            _payPalCurrencyCodeParser = payPalCurrencyCodeParser;
        }

        public CreateRecurringPaymentsProfileRequestDetailsType GetCreateRecurringPaymentProfileRequestDetails(
            ProcessPaymentRequest processPaymentRequest)
        {
            var details = new CreateRecurringPaymentsProfileRequestDetailsType
            {
                Token = processPaymentRequest.CustomValues[Defaults.PaypalTokenKey].ToString()
            };

            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);

            if (customer is null)
                throw new NopException("Customer is not found");

            if ((customer.BillingAddressId ?? 0) == 0)
                throw new NopException("Customer doesn't have a billing address");

            var billingAddress = _addressService.GetAddressById(customer.BillingAddressId.Value);

            var country = _countryService.GetCountryByAddress(billingAddress);

            details.CreditCard = new CreditCardDetailsType
            {
                CreditCardNumber = processPaymentRequest.CreditCardNumber,
                CreditCardType = GetPaypalCreditCardType(processPaymentRequest.CreditCardType),
                ExpMonthSpecified = true,
                ExpMonth = processPaymentRequest.CreditCardExpireMonth,
                ExpYearSpecified = true,
                ExpYear = processPaymentRequest.CreditCardExpireYear,
                CVV2 = processPaymentRequest.CreditCardCvv2,
                CardOwner = new PayerInfoType
                {
                    PayerCountry = GetPaypalCountryCodeType(country)
                },
                CreditCardTypeSpecified = true
            };



            details.CreditCard.CardOwner.Address = new AddressType
            {
                CountrySpecified = true,
                Street1 = billingAddress.Address1,
                Street2 = billingAddress.Address2,
                CityName = billingAddress.City,
                StateOrProvince = _stateProvinceService.GetStateProvinceByAddress(billingAddress)?.Abbreviation ?? "CA",
                Country = GetPaypalCountryCodeType(country),
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
            var store = _storeService.GetStoreById(processPaymentRequest.StoreId);
            var storeName = store == null ? string.Empty : store.Name;
            details.ScheduleDetails.Description = $"{storeName} - recurring payment";
            var currencyCodeType = _payPalCurrencyCodeParser.GetCurrencyCodeType(_workContext.WorkingCurrency);
            details.ScheduleDetails.PaymentPeriod = new BillingPeriodDetailsType
            {
                Amount = processPaymentRequest.OrderTotal.GetBasicAmountType(currencyCodeType),
                BillingFrequency = processPaymentRequest.RecurringCycleLength
            };

            details.ScheduleDetails.PaymentPeriod.BillingPeriod = processPaymentRequest.RecurringCyclePeriod switch
            {
                RecurringProductCyclePeriod.Days => BillingPeriodType.Day,
                RecurringProductCyclePeriod.Weeks => BillingPeriodType.Week,
                RecurringProductCyclePeriod.Months => BillingPeriodType.Month,
                RecurringProductCyclePeriod.Years => BillingPeriodType.Year,
                _ => throw new NopException("Not supported cycle period"),
            };
            details.ScheduleDetails.PaymentPeriod.TotalBillingCycles = processPaymentRequest.RecurringTotalCycles;
            details.ScheduleDetails.PaymentPeriod.TotalBillingCyclesSpecified = true;

            return details;
        }

        protected CountryCodeType GetPaypalCountryCodeType(Country country)
        {
            if (country is null)
                throw new ArgumentException(nameof(country));

            Enum.TryParse(country.TwoLetterIsoCode, out CountryCodeType payerCountry);

            return payerCountry;
        }

        protected CreditCardTypeType GetPaypalCreditCardType(string creditCardType)
        {
            Enum.TryParse(creditCardType, out CreditCardTypeType creditCardTypeType);

            return creditCardTypeType;
        }
    }
}