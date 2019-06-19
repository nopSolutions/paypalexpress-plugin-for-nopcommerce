using System;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Customers;
using Nop.Services.Payments;
using Nop.Services.Stores;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalRecurringPaymentsService : IPayPalRecurringPaymentsService
    {
        private readonly ICustomerService _customerService;
        private readonly IStoreService _storeService;
        private readonly IPayPalCurrencyCodeParser _payPalCurrencyCodeParser;
        private readonly IWorkContext _workContext;

        public PayPalRecurringPaymentsService(ICustomerService customerService,
            IStoreService storeService,
            IPayPalCurrencyCodeParser payPalCurrencyCodeParser,
            IWorkContext workContext)
        {
            _customerService = customerService;
            _storeService = storeService;
            _payPalCurrencyCodeParser = payPalCurrencyCodeParser;
            _workContext = workContext;
        }

        public CreateRecurringPaymentsProfileRequestDetailsType GetCreateRecurringPaymentProfileRequestDetails(
            ProcessPaymentRequest processPaymentRequest)
        {
            var details = new CreateRecurringPaymentsProfileRequestDetailsType
            {
                Token = processPaymentRequest.CustomValues["PaypalToken"].ToString()
            };

            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
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
                    PayerCountry = GetPaypalCountryCodeType(customer.BillingAddress.Country)
                },
                CreditCardTypeSpecified = true
            };

            details.CreditCard.CardOwner.Address = new AddressType
            {
                CountrySpecified = true,
                Street1 = customer.BillingAddress.Address1,
                Street2 = customer.BillingAddress.Address2,
                CityName = customer.BillingAddress.City,
                StateOrProvince = customer.BillingAddress.StateProvince != null
                    ? customer.BillingAddress.StateProvince.Abbreviation
                    : "CA",
                Country = GetPaypalCountryCodeType(customer.BillingAddress.Country),
                PostalCode = customer.BillingAddress.ZipPostalCode
            };
            details.CreditCard.CardOwner.Payer = customer.BillingAddress.Email;
            details.CreditCard.CardOwner.PayerName = new PersonNameType
            {
                FirstName = customer.BillingAddress.FirstName,
                LastName = customer.BillingAddress.LastName
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

            switch (processPaymentRequest.RecurringCyclePeriod)
            {
                case RecurringProductCyclePeriod.Days:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Day;
                    break;
                case RecurringProductCyclePeriod.Weeks:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Week;
                    break;
                case RecurringProductCyclePeriod.Months:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Month;
                    break;
                case RecurringProductCyclePeriod.Years:
                    details.ScheduleDetails.PaymentPeriod.BillingPeriod = BillingPeriodType.Year;
                    break;
                default:
                    throw new NopException("Not supported cycle period");
            }

            details.ScheduleDetails.PaymentPeriod.TotalBillingCycles = processPaymentRequest.RecurringTotalCycles;
            details.ScheduleDetails.PaymentPeriod.TotalBillingCyclesSpecified = true;

            return details;
        }

        protected CountryCodeType GetPaypalCountryCodeType(Country country)
        {
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