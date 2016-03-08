using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalRedirectionService
    {
        string ProcessSubmitButton(IList<ShoppingCartItem> cart, TempDataDictionary tempData);
        bool ProcessReturn(string token);
    }
}