using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Customers;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutService
    {
        private readonly ICustomerService _customerService;
        private readonly IOrderService _orderService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly OrderSettings _orderSettings;

        public PayPalExpressCheckoutService(ICustomerService customerService,
            IOrderService orderService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IWorkContext workContext,
            OrderSettings orderSettings)
        {
            _customerService = customerService;
            _orderService = orderService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _workContext = workContext;
            _orderSettings = orderSettings;
        }

        public IList<ShoppingCartItem> GetCart()
        {
            return _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);
        }

        public bool IsAllowedToCheckout()
        {
            return !(_customerService.IsGuest(_workContext.CurrentCustomer) && !_orderSettings.AnonymousCheckoutAllowed);
        }

        public bool IsMinimumOrderPlacementIntervalValid(Customer customer)
        {
            //prevent 2 orders being placed within an X seconds time frame
            if (_orderSettings.MinimumOrderPlacementInterval == 0)
                return true;

            var lastOrder = _orderService.SearchOrders(_storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1)
                .FirstOrDefault();
            if (lastOrder == null)
                return true;

            var interval = DateTime.UtcNow - lastOrder.CreatedOnUtc;

            return interval.TotalSeconds > _orderSettings.MinimumOrderPlacementInterval;
        }

        public IEnumerable<SelectListItem> GetPaymentActionOptions(PaymentActionCodeType paymentAction)
        {
            return new List<PaymentActionCodeType> { PaymentActionCodeType.Authorization, PaymentActionCodeType.Sale }
                .Select(type => new SelectListItem
                {
                    Selected = type == paymentAction,
                    Text = type.ToString(),
                    Value = type.ToString()
                });
        }

        public IEnumerable<SelectListItem> GetLocaleCodeOptions(string localeCode)
        {
            var localeOptions = new Dictionary<string, string>
            {
                { "AU", "Australia" },
                { "AI", "Austria" },
                { "BE", "Belgium" },
                { "BR", "Brazil" },
                { "CA", "Canada" },
                { "CH", "Switzerland" },
                { "CN", "China" },
                { "DE", "Germany" },
                { "ES", "Spain" },
                { "GB", "United Kingdom" },
                { "FR", "France" },
                { "IT", "Italy" },
                { "NL", "Netherlands" },
                { "PL", "Poland" },
                { "PT", "Portugal" },
                { "RU", "Russia" },
                { "da_DK", "Danish (for Denmark only)" },
                { "he_IL", "Hebrew (all)" },
                { "id_ID", "Indonesian (for Indonesia only)" },
                { "jp_JP", "Japanese (for Japan only)" },
                { "no_NO", "Norweigan (for Norway only)" },
                { "pt_BR", "Portuguese (for Portugal and Brazil only)" },
                { "ru_RU", "Russian (for Lithuania, Latvia, and Ukraine only)" },
                { "sv_SE", "Swedish (for Sweden only)" },
                { "th_TH", "Thai (for Thailand only)" },
                { "tr_TR", "Turkish (for Turkey only)" },
                { "zh_CN", "Simplified Chinese (for China only)" },
                { "zh_HK", "Traditional Chinese (for Hong Kong only)" },
                { "zh_TW", "Traditional Chinese (for Taiwan only)" }
            }.Select(type => new SelectListItem
            {
                Selected = type.Key == localeCode,
                Text = type.Value,
                Value = type.Key
            }).OrderBy(item => item.Text).ToList();
            localeOptions.Insert(0, new SelectListItem
            {
                Text = "United States",
                Value = "US",
                Selected = localeCode == null || localeCode == "US"
            });

            return localeOptions;
        }
    }
}