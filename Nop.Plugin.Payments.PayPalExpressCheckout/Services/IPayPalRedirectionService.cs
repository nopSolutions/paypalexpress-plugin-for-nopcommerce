using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalRedirectionService
    {
        string ProcessSubmitButton(IList<ShoppingCartItem> cart, ITempDataDictionary tempData);

        bool ProcessReturn(string token);
    }
}