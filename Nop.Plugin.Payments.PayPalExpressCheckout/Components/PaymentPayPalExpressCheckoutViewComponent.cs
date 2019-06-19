using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Components
{
    [ViewComponent(Name = "PaymentPayPalExpressCheckout")]
    public class PaymentPayPalExpressCheckoutViewComponent : NopViewComponent
    {
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPayPalExpressCheckoutService _payPalExpressCheckoutService;

        public PaymentPayPalExpressCheckoutViewComponent(IOrderProcessingService orderProcessingService,
            IPayPalExpressCheckoutService payPalExpressCheckoutService)
        {
            _orderProcessingService = orderProcessingService;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
        }

        public IViewComponentResult Invoke()
        {
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return Content(string.Empty);

            var minOrderSubtotalAmountOk = _orderProcessingService.ValidateMinOrderSubtotalAmount(cart);
            if (!minOrderSubtotalAmountOk)
                return Content(string.Empty);

            var model = new PaymentInfoModel
            {
                ButtonImageLocation = "https://www.paypalobjects.com/en_GB/i/btn/btn_xpressCheckout.gif"
            };

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentInfo.cshtml", model);
        }
    }
}
