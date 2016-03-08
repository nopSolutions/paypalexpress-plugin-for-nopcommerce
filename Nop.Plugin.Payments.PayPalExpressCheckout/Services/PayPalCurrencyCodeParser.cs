using System;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalCurrencyCodeParser : IPayPalCurrencyCodeParser
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
            CurrencyCodeType code;
            return Enum.TryParse(currencyCode, out code)
                       ? code
                       : CurrencyCodeType.CustomCode;
        }
    }
}