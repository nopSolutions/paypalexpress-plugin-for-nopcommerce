using Nop.Core.Domain.Directory;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalCurrencyCodeParser
    {
        CurrencyCodeType GetCurrencyCodeType(Currency workingCurrency);
        CurrencyCodeType GetCurrencyCodeType(string code);
    }
}