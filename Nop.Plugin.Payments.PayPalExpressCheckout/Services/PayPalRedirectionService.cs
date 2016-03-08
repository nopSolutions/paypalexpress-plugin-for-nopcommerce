using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
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
        private readonly HttpSessionStateBase _session;

        public PayPalRedirectionService(IPayPalInterfaceService payPalInterfaceService,
                                        IPayPalSecurityService payPalSecurityService,
                                        IPayPalRequestService payPalRequestService,
                                        IPayPalUrlService payPalUrlService,
                                        ILogger logger,
                                        IWebHelper webHelper,
                                        IPayPalCheckoutDetailsService payPalCheckoutDetailsService,
                                        IWorkContext workContext,
                                        ICustomerService customerService,
                                        HttpSessionStateBase session)
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
            _session = session;
        }

        public string ProcessSubmitButton(IList<ShoppingCartItem> cart, TempDataDictionary tempData)
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
                                                                                string.Join(", ", setExpressCheckoutResponse.Errors.Select(
                                                                                    errorType => errorType.ErrorCode + ": " + errorType.LongMessage)));
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
                                                                                   _payPalRequestService
                                                                                       .GetGetExpressCheckoutDetailsRequest
                                                                                       (token));

                details.LogResponse(Guid.Empty);
                if (details.Ack == AckCodeType.Success || details.Ack == AckCodeType.SuccessWithWarning)
                {
                    var request =
                        _payPalCheckoutDetailsService.SetCheckoutDetails(
                            details.GetExpressCheckoutDetailsResponseDetails);
                    _session["OrderPaymentInfo"] = request;

                    var customer = _customerService.GetCustomerById(request.CustomerId);

                    _workContext.CurrentCustomer = customer;
                    _customerService.UpdateCustomer(_workContext.CurrentCustomer);
                    return true;
                }
                return false;
            }
        }
    }
}