using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Components
{
    [ViewComponent(Name = "PaymentPayPalExpressCheckout")]
    public class PaymentPayPalExpressCheckoutViewComponent : NopViewComponent
    {
        private readonly AddressSettings _addressSettings;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPayPalExpressCheckoutService _payPalExpressCheckoutService;
        private readonly IWorkContext _workContext;

        public PaymentPayPalExpressCheckoutViewComponent(AddressSettings addressSettings,
            IOrderProcessingService orderProcessingService,
            IPaymentPluginManager paymentPluginManager,
            IPayPalExpressCheckoutService payPalExpressCheckoutService,
            IWorkContext workContext)
        {
            _addressSettings = addressSettings;
            _orderProcessingService = orderProcessingService;
            _paymentPluginManager = paymentPluginManager;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
            _workContext = workContext;
        }

        public IViewComponentResult Invoke()
        {
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return Content(string.Empty);

            var minOrderSubtotalAmountOk = _orderProcessingService.ValidateMinOrderSubtotalAmount(cart);
            if (!minOrderSubtotalAmountOk)
                return Content(string.Empty);
            
            var filterByCountryId = 0;
            if (_addressSettings.CountryEnabled && _workContext.CurrentCustomer.BillingAddress?.Country != null)
                filterByCountryId = _workContext.CurrentCustomer.BillingAddress.Country.Id;

            var plugin = _paymentPluginManager.LoadPluginBySystemName("Payments.PayPalExpressCheckout");

            if (plugin == null || _paymentPluginManager.GetRestrictedCountryIds(plugin).Contains(filterByCountryId))
                return Content(string.Empty);

            var model = new PaymentInfoModel
            {
                ButtonImageLocation = "https://paypalobjects.com/en_GB/i/btn/btn_xpressCheckout.gif"
            };

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentInfo.cshtml", model);
        }
    }
}
