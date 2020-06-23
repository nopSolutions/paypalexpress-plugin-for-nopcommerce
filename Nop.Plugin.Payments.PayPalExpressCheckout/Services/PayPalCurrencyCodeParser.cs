using System;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalCurrencyCodeParser
    {
        public CurrencyCodeType GetCurrencyCodeType(Currency workingCurrency)
        {
            return GetCode(workingCurrency.CurrencyCode);
        }

        public CurrencyCodeType GetCurrencyCodeType(string code)
        {
            return GetCode(code);
        }

        private CurrencyCodeType GetCode(string currencyCode)
        {
            return Enum.TryParse(currencyCode, out CurrencyCodeType code)
                ? code
                : CurrencyCodeType.CustomCode;
        }
    }
}