using System.Collections.Generic;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutConfirmOrderService : IPayPalExpressCheckoutConfirmOrderService
    {
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ICurrencyService _currencyService;
        private readonly OrderSettings _orderSettings;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly IPriceFormatter _priceFormatter;

        public PayPalExpressCheckoutConfirmOrderService(
            IOrderProcessingService orderProcessingService,
            ICurrencyService currencyService,
            OrderSettings orderSettings,
            IWorkContext workContext,
            ILocalizationService localizationService,
            IPriceFormatter priceFormatter)
        {
            _orderProcessingService = orderProcessingService;
            _currencyService = currencyService;
            _orderSettings = orderSettings;
            _workContext = workContext;
            _localizationService = localizationService;
            _priceFormatter = priceFormatter;
        }

        public CheckoutConfirmModel PrepareConfirmOrderModel(IList<ShoppingCartItem> cart)
        {
            var model = new CheckoutConfirmModel();
            //min order amount validation
            bool minOrderTotalAmountOk = _orderProcessingService.ValidateMinOrderTotalAmount(cart);
            if (!minOrderTotalAmountOk)
            {
                decimal minOrderTotalAmount = _currencyService.ConvertFromPrimaryStoreCurrency(_orderSettings.MinOrderTotalAmount, _workContext.WorkingCurrency);
                model.MinOrderTotalWarning = string.Format(_localizationService.GetResource("Checkout.MinOrderTotalAmount"), _priceFormatter.FormatPrice(minOrderTotalAmount, true, false));
            }
            return model;
        }
    }
}