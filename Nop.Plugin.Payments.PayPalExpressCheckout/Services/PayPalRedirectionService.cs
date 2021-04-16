using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class PayPalRedirectionService
    {
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly ISession _session;
        private readonly PaymentSettings _paymentSettings;
        private readonly PayPalCheckoutDetailsService _payPalCheckoutDetailsService;
        private readonly PayPalInterfaceService _payPalInterfaceService;
        private readonly PayPalSecurityService _payPalSecurityService;
        private readonly PayPalRequestService _payPalRequestService;
        private readonly PayPalUrlService _payPalUrlService;

        public PayPalRedirectionService(ILogger logger,
            IWebHelper webHelper,
            IWorkContext workContext,
            ICustomerService customerService,
            IHttpContextAccessor httpContextAccessor,
            PaymentSettings paymentSettings,
            PayPalCheckoutDetailsService payPalCheckoutDetailsService,
            PayPalInterfaceService payPalInterfaceService,
            PayPalSecurityService payPalSecurityService,
            PayPalRequestService payPalRequestService,
            PayPalUrlService payPalUrlService)
        {
            _logger = logger;
            _webHelper = webHelper;
            _workContext = workContext;
            _customerService = customerService;
            _session = httpContextAccessor.HttpContext.Session;
            _paymentSettings = paymentSettings;
            _payPalCheckoutDetailsService = payPalCheckoutDetailsService;
            _payPalInterfaceService = payPalInterfaceService;
            _payPalSecurityService = payPalSecurityService;
            _payPalRequestService = payPalRequestService;
            _payPalUrlService = payPalUrlService;
        }

        public async Task<string> ProcessSubmitButtonAsync(IList<ShoppingCartItem> cart, ITempDataDictionary tempData)
        {
            using var payPalApiaaInterface = _payPalInterfaceService.GetAAService();
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();

            var setExpressCheckoutResponse = payPalApiaaInterface.SetExpressCheckout(
                ref customSecurityHeaderType, await _payPalRequestService.GetSetExpressCheckoutRequestAsync(cart));

            var result = new ProcessPaymentResult();
            var redirectUrl = string.Empty;
            await setExpressCheckoutResponse.HandleResponseAsync(result,
                (paymentResult, type) =>
                {
                    var token = setExpressCheckoutResponse.Token;
                    redirectUrl = _payPalUrlService.GetExpressCheckoutRedirectUrl(token);
                },
                (paymentResult, type) =>
                {
                    _logger.InsertLogAsync(LogLevel.Error, "Error passing cart to PayPal",
                        string.Join(", ", setExpressCheckoutResponse.Errors.Select(errorType => errorType.ErrorCode + ": " + errorType.LongMessage))).Wait();
                    tempData[Defaults.CheckoutErrorMessageKey] = "An error occurred setting up your cart for PayPal.";
                    redirectUrl = _webHelper.GetUrlReferrer();
                }, Guid.Empty);

            return redirectUrl;
        }

        public async Task<bool> ProcessReturnAsync(string token)
        {
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            var details = payPalApiaaInterfaceClient.GetExpressCheckoutDetails(ref customSecurityHeaderType,
                _payPalRequestService.GetGetExpressCheckoutDetailsRequest(token));

            await details.LogResponseAsync(Guid.Empty);
            if (details.Ack != AckCodeType.Success && details.Ack != AckCodeType.SuccessWithWarning)
                return false;

            var request =
                await _payPalCheckoutDetailsService.SetCheckoutDetailsAsync(
                    details.GetExpressCheckoutDetailsResponseDetails);

            //set previous order GUID (if exists)
            GenerateOrderGuid(request);

            _session.Set(Defaults.ProcessPaymentRequestKey, request);

            var customer = await _customerService.GetCustomerByIdAsync(request.CustomerId);

            await _workContext.SetCurrentCustomerAsync(customer);
            await _customerService.UpdateCustomerAsync(await _workContext.GetCurrentCustomerAsync());
            return true;
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
            var previousPaymentRequest = _session.Get<ProcessPaymentRequest>(Defaults.ProcessPaymentRequestKey);
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