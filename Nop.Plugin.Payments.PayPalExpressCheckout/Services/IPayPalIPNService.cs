using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalIPNService
    {
        void HandleIPN(string ipnData);
    }
}