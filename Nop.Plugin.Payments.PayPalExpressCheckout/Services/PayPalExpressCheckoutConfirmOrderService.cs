using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutConfirmOrderService
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

        public async Task<CheckoutConfirmModel> PrepareConfirmOrderModelAsync(IList<ShoppingCartItem> cart)
        {
            var model = new CheckoutConfirmModel();
            //min order amount validation
            var minOrderTotalAmountOk = await _orderProcessingService.ValidateMinOrderTotalAmountAsync(cart);
            if (minOrderTotalAmountOk)
                return model;

            var minOrderTotalAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(_orderSettings.MinOrderTotalAmount, await _workContext.GetWorkingCurrencyAsync());
            model.MinOrderTotalWarning = string.Format(await _localizationService.GetResourceAsync("Checkout.MinOrderTotalAmount"), await _priceFormatter.FormatPriceAsync(minOrderTotalAmount, true, false));

            return model;
        }
    }
}