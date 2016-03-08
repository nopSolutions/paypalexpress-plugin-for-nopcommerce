using System.IO;
using Nop.Services.Stores;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalUrlService
    {
        string GetReturnURL();
        string GetCancelURL();
        string GetCallbackURL();
        string GetCallbackTimeout();
        string GetExpressCheckoutRedirectUrl(string token);
    }
}