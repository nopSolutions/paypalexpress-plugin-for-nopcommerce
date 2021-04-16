using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutPlaceOrderService
    {
        private readonly ISession _session;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentService _paymentService;
        private readonly IWebHelper _webHelper;
        private readonly ILogger _logger;
        private readonly PayPalExpressCheckoutService _payPalExpressCheckoutService;

        public PayPalExpressCheckoutPlaceOrderService(IHttpContextAccessor httpContextAccessor,
            IWorkContext workContext,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            IOrderProcessingService orderProcessingService,
            IPaymentService paymentService,
            IWebHelper webHelper,
            ILogger logger,
            PayPalExpressCheckoutService payPalExpressCheckoutService)
        {
            _session = httpContextAccessor.HttpContext.Session;
            _workContext = workContext;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _orderProcessingService = orderProcessingService;
            _paymentService = paymentService;
            _webHelper = webHelper;
            _logger = logger;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
        }

        public async Task<CheckoutPlaceOrderModel> PlaceOrderAsync()
        {
            var model = new CheckoutPlaceOrderModel();
            try
            {
                var processPaymentRequest = _session.Get<ProcessPaymentRequest>(Defaults.ProcessPaymentRequestKey);

                if (processPaymentRequest == null)
                {
                    model.RedirectToCart = true;
                    return model;
                }

                //prevent 2 orders being placed within an X seconds time frame
                if (!await _payPalExpressCheckoutService.IsMinimumOrderPlacementIntervalValidAsync(await _workContext.GetCurrentCustomerAsync()))
                    throw new Exception(await _localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval"));

                //place order
                processPaymentRequest.StoreId = (await _storeContext.GetCurrentStoreAsync()).Id;
                processPaymentRequest.CustomerId = (await _workContext.GetCurrentCustomerAsync()).Id;
                processPaymentRequest.PaymentMethodSystemName = "Payments.PayPalExpressCheckout";
                var placeOrderResult = await _orderProcessingService.PlaceOrderAsync(processPaymentRequest);

                if (placeOrderResult.Success)
                {
                    var doExpressCheckoutPaymentResponseType = _session.Get<DoExpressCheckoutPaymentResponseType>(Defaults.CheckoutPaymentResponseTypeKey);
                    
                    if (doExpressCheckoutPaymentResponseType != null)
                        await doExpressCheckoutPaymentResponseType.LogOrderNotesAsync(placeOrderResult.PlacedOrder.OrderGuid);
                    
                    _session.Remove(Defaults.ProcessPaymentRequestKey);
                    var postProcessPaymentRequest = new PostProcessPaymentRequest
                    {
                        Order = placeOrderResult.PlacedOrder
                    };
                    await _paymentService.PostProcessPaymentAsync(postProcessPaymentRequest);

                    if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                    {
                        //redirection or POST has been done in PostProcessPayment
                        model.IsRedirected = true;
                        return model;
                    }

                    model.CompletedId = placeOrderResult.PlacedOrder.Id;
                    return model;
                }

                foreach (var error in placeOrderResult.Errors)
                    model.Warnings.Add(error);
            }
            catch (Exception exc)
            {
                await _logger.WarningAsync(exc.Message, exc);
                model.Warnings.Add(exc.Message);
            }

            return model;
        }
    }
}