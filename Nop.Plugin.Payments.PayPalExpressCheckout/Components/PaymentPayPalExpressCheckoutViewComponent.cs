using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Common;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Components
{
    [ViewComponent(Name = "PaymentPayPalExpressCheckout")]
    public class PaymentPayPalExpressCheckoutViewComponent : NopViewComponent
    {
        private readonly AddressSettings _addressSettings;
        private readonly IAddressService _addressService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IWorkContext _workContext;
        private readonly PayPalExpressCheckoutService _payPalExpressCheckoutService;

        public PaymentPayPalExpressCheckoutViewComponent(AddressSettings addressSettings,
            IAddressService addressService,
            IOrderProcessingService orderProcessingService,
            IPaymentPluginManager paymentPluginManager,
            IWorkContext workContext,
            PayPalExpressCheckoutService payPalExpressCheckoutService)
        {
            _addressSettings = addressSettings;
            _addressService = addressService;
            _orderProcessingService = orderProcessingService;
            _paymentPluginManager = paymentPluginManager;
            _workContext = workContext;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
        }

        public IViewComponentResult Invoke()
        {
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return Content(string.Empty);

            if (!_orderProcessingService.ValidateMinOrderSubtotalAmount(cart))
                return Content(string.Empty);

            var filterByCountryId = 0;
            var billingAddress = _addressService.GetAddressById(_workContext.CurrentCustomer.BillingAddressId ?? 0);

            if (_addressSettings.CountryEnabled && billingAddress?.CountryId != null)
                filterByCountryId = billingAddress.CountryId.Value;

            var plugin = _paymentPluginManager.LoadPluginBySystemName("Payments.PayPalExpressCheckout");

            if (plugin == null || _paymentPluginManager.GetRestrictedCountryIds(plugin).Contains(filterByCountryId))
                return Content(string.Empty);

            var model = new PaymentInfoModel
            {
                ButtonImageLocation = Defaults.CheckoutButtonImageUrl
            };

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentInfo.cshtml", model);
        }
    }
}
