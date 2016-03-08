using System;
using System.Collections.Generic;
using System.Web;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalExpressCheckoutPlaceOrderService : IPayPalExpressCheckoutPlaceOrderService
    {
        private readonly HttpSessionStateBase _session;
        private readonly IPayPalExpressCheckoutService _payPalExpressCheckoutService;
        private readonly IWorkContext _workContext;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentService _paymentService;
        private readonly IWebHelper _webHelper;
        private readonly ILogger _logger;

        public PayPalExpressCheckoutPlaceOrderService(HttpSessionStateBase session,
                                                      IPayPalExpressCheckoutService payPalExpressCheckoutService,
                                                      IWorkContext workContext,
                                                      ILocalizationService localizationService,
                                                      IStoreContext storeContext,
                                                      IOrderProcessingService orderProcessingService,
                                                      IPaymentService paymentService,
                                                      IWebHelper webHelper,
                                                      ILogger logger)
        {
            _session = session;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
            _workContext = workContext;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _orderProcessingService = orderProcessingService;
            _paymentService = paymentService;
            _webHelper = webHelper;
            _logger = logger;
        }

        public CheckoutPlaceOrderModel PlaceOrder()
        {
            var model = new CheckoutPlaceOrderModel();
            try
            {
                var processPaymentRequest = _session["OrderPaymentInfo"] as ProcessPaymentRequest;
                if (processPaymentRequest == null)
                {
                    model.RedirectToCart = true;
                    return model;
                }

                //prevent 2 orders being placed within an X seconds time frame
                if (!_payPalExpressCheckoutService.IsMinimumOrderPlacementIntervalValid(_workContext.CurrentCustomer))
                    throw new Exception(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));

                //place order
                processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
                processPaymentRequest.CustomerId = _workContext.CurrentCustomer.Id;
                processPaymentRequest.PaymentMethodSystemName = "Payments.PayPalExpressCheckout";
                var placeOrderResult = _orderProcessingService.PlaceOrder(processPaymentRequest);

                if (placeOrderResult.Success)
                {
                    var doExpressCheckoutPaymentResponseType = _session["express-checkout-response-type"] as DoExpressCheckoutPaymentResponseType;
                    if (doExpressCheckoutPaymentResponseType != null)
                    {
                        doExpressCheckoutPaymentResponseType.LogOrderNotes(placeOrderResult.PlacedOrder.OrderGuid);
                    }
                    _session["OrderPaymentInfo"] = null;
                    var postProcessPaymentRequest = new PostProcessPaymentRequest
                                                        {
                                                            Order = placeOrderResult.PlacedOrder
                                                        };
                    _paymentService.PostProcessPayment(postProcessPaymentRequest);

                    if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                    {
                        //redirection or POST has been done in PostProcessPayment
                        model.IsRedirected = true;
                        return model;
                    }
                    else
                    {
                        model.CompletedId = placeOrderResult.PlacedOrder.Id;
                        return model;
                    }
                }
                else
                {
                    foreach (var error in placeOrderResult.Errors)
                        model.Warnings.Add(error);
                }
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc);
                model.Warnings.Add(exc.Message);
            }
            return model;
        }
    }
}