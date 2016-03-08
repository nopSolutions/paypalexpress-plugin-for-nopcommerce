using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalExpressCheckoutService
    {
        IList<ShoppingCartItem> GetCart();
        bool IsAllowedToCheckout();
        bool IsMinimumOrderPlacementIntervalValid(Customer customer);
        IEnumerable<SelectListItem> GetPaymentActionOptions(PaymentActionCodeType paymentAction);
        IEnumerable<SelectListItem> GetLocaleCodeOptions(string localeCode);
    }
}