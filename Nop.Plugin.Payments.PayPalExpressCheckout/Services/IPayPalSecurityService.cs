using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalSecurityService
    {
        CustomSecurityHeaderType GetRequesterCredentials();
    }
}