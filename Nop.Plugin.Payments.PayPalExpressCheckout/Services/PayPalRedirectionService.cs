using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalRedirectionService : IPayPalRedirectionService
    {
        private readonly IPayPalInterfaceService _payPalInterfaceService;
        private readonly IPayPalSecurityService _payPalSecurityService;
        private readonly IPayPalRequestService _payPalRequestService;
        private readonly IPayPalUrlService _payPalUrlService;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly IPayPalCheckoutDetailsService _payPalCheckoutDetailsService;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly ISession _session;
        private readonly PaymentSettings _paymentSettings;

        public PayPalRedirectionService(IPayPalInterfaceService payPalInterfaceService,
            IPayPalSecurityService payPalSecurityService,
            IPayPalRequestService payPalRequestService,
            IPayPalUrlService payPalUrlService,
            ILogger logger,
            IWebHelper webHelper,
            IPayPalCheckoutDetailsService payPalCheckoutDetailsService,
            IWorkContext workContext,
            ICustomerService customerService,
            IHttpContextAccessor httpContextAccessor,
            PaymentSettings paymentSettings)
        {
            _payPalInterfaceService = payPalInterfaceService;
            _payPalSecurityService = payPalSecurityService;
            _payPalRequestService = payPalRequestService;
            _payPalUrlService = payPalUrlService;
            _logger = logger;
            _webHelper = webHelper;
            _payPalCheckoutDetailsService = payPalCheckoutDetailsService;
            _workContext = workContext;
            _customerService = customerService;
            _session = httpContextAccessor.HttpContext.Session;
            _paymentSettings = paymentSettings;
        }

        public string ProcessSubmitButton(IList<ShoppingCartItem> cart, ITempDataDictionary tempData)
        {
            using (var payPalApiaaInterface = _payPalInterfaceService.GetAAService())
            {
                var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();

                var setExpressCheckoutResponse = payPalApiaaInterface.SetExpressCheckout(
                    ref customSecurityHeaderType, _payPalRequestService.GetSetExpressCheckoutRequest(cart));

                var result = new ProcessPaymentResult();
                var redirectUrl = string.Empty;
                setExpressCheckoutResponse.HandleResponse(result,
                    (paymentResult, type) =>
                    {
                        var token = setExpressCheckoutResponse.Token;
                        redirectUrl = _payPalUrlService.GetExpressCheckoutRedirectUrl(token);
                    },
                    (paymentResult, type) =>
                    {
                        _logger.InsertLog(LogLevel.Error, "Error passing cart to PayPal",
                            string.Join(", ", setExpressCheckoutResponse.Errors.Select(errorType => errorType.ErrorCode + ": " + errorType.LongMessage)));
                        tempData["paypal-ec-error"] = "An error occurred setting up your cart for PayPal.";
                        redirectUrl = _webHelper.GetUrlReferrer();
                    }, Guid.Empty);

                return redirectUrl;
            }
        }

        public bool ProcessReturn(string token)
        {
            using (var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService())
            {
                var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
                var details = payPalApiaaInterfaceClient.GetExpressCheckoutDetails(ref customSecurityHeaderType,
                    _payPalRequestService.GetGetExpressCheckoutDetailsRequest(token));

                details.LogResponse(Guid.Empty);
                if (details.Ack != AckCodeType.Success && details.Ack != AckCodeType.SuccessWithWarning)
                    return false;

                var request =
                    _payPalCheckoutDetailsService.SetCheckoutDetails(
                        details.GetExpressCheckoutDetailsResponseDetails);

                //set previous order GUID (if exists)
                GenerateOrderGuid(request);

                _session.Set("OrderPaymentInfo", request);

                var customer = _customerService.GetCustomerById(request.CustomerId);

                _workContext.CurrentCustomer = customer;
                _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                return true;
            }
        }

        /// <summary>
        /// Generate an order GUID
        /// </summary>
        /// <param name="processPaymentRequest">Process payment request</param>
        protected virtual void GenerateOrderGuid(ProcessPaymentRequest processPaymentRequest)
        {
            if (processPaymentRequest == null)
                return;

            //we should use the same GUID for multiple payment attempts
            //this way a payment gateway can prevent security issues such as credit card brute-force attacks
            //in order to avoid any possible limitations by payment gateway we reset GUID periodically
            var previousPaymentRequest = _session.Get<ProcessPaymentRequest>("OrderPaymentInfo");
            if (_paymentSettings.RegenerateOrderGuidInterval > 0 &&
                (previousPaymentRequest?.OrderGuidGeneratedOnUtc.HasValue ?? false))
            {
                var interval = DateTime.UtcNow - previousPaymentRequest.OrderGuidGeneratedOnUtc.Value;
                if (interval.TotalSeconds < _paymentSettings.RegenerateOrderGuidInterval)
                {
                    processPaymentRequest.OrderGuid = previousPaymentRequest.OrderGuid;
                    processPaymentRequest.OrderGuidGeneratedOnUtc = previousPaymentRequest.OrderGuidGeneratedOnUtc;
                }
            }

            if (processPaymentRequest.OrderGuid == Guid.Empty)
            {
                processPaymentRequest.OrderGuid = Guid.NewGuid();
                processPaymentRequest.OrderGuidGeneratedOnUtc = DateTime.UtcNow;
            }
        }
    }
}