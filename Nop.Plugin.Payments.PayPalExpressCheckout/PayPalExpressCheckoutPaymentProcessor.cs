using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.PayPalExpressCheckout.Controllers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;

namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    public class PayPalExpressCheckoutPaymentProcessor : BasePlugin, IPaymentMethod
    {
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
        private readonly IPayPalInterfaceService _payPalInterfaceService;
        private readonly IPayPalSecurityService _payPalSecurityService;
        private readonly IPayPalRequestService _payPalRequestService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly HttpSessionStateBase _session;

        public PayPalExpressCheckoutPaymentProcessor(PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
        IPayPalInterfaceService payPalInterfaceService,
        IPayPalSecurityService payPalSecurityService,
        IPayPalRequestService payPalRequestService,
        IOrderTotalCalculationService orderTotalCalculationService,
        ISettingService settingService,
        HttpSessionStateBase session)
        {
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
            _payPalInterfaceService = payPalInterfaceService;
            _payPalSecurityService = payPalSecurityService;
            _payPalRequestService = payPalRequestService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _settingService = settingService;
            _session = session;
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using (var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService())
            {
                var doExpressCheckoutPaymentResponseType =
                    payPalApiaaInterfaceClient.DoExpressCheckoutPayment(ref customSecurityHeaderType,
                                                                        _payPalRequestService.GetDoExpressCheckoutPaymentRequest(processPaymentRequest));
                _session["express-checkout-response-type"] = doExpressCheckoutPaymentResponseType;

                return doExpressCheckoutPaymentResponseType.HandleResponse(new ProcessPaymentResult(),
                (paymentResult, type) =>
                {
                    paymentResult.NewPaymentStatus =
                    _payPalExpressCheckoutPaymentSettings.PaymentAction == PaymentActionCodeType.Authorization
                           ? PaymentStatus.Authorized
                           : PaymentStatus.Paid;

                    paymentResult.AuthorizationTransactionId =
                    processPaymentRequest.CustomValues["PaypalToken"].ToString();
                    var paymentInfoType = type.DoExpressCheckoutPaymentResponseDetails.PaymentInfo.FirstOrDefault();
                    if (paymentInfoType != null)
                    {
                        paymentResult.CaptureTransactionId = paymentInfoType.TransactionID;

                    }
                    paymentResult.CaptureTransactionResult = type.Ack.ToString();
                },
                (paymentResult, type) =>
                {
                    paymentResult.NewPaymentStatus = PaymentStatus.Pending;
                    type.Errors.AddErrors(paymentResult.AddError);
                    paymentResult.AddError(type.DoExpressCheckoutPaymentResponseDetails.RedirectRequired);
                }, processPaymentRequest.OrderGuid);
            }
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _payPalExpressCheckoutPaymentSettings.HandlingFee, _payPalExpressCheckoutPaymentSettings.HandlingFeeIsPercentage);
            return result;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using (var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService())
            {
                var doCaptureReq = _payPalRequestService.GetDoCaptureRequest(capturePaymentRequest);
                var response = payPalApiaaInterfaceClient.DoCapture(ref customSecurityHeaderType, doCaptureReq);

                return response.HandleResponse(new CapturePaymentResult
                                                   {
                                                       CaptureTransactionId =
                                                           capturePaymentRequest.Order.CaptureTransactionId
                                                   },
                                               (paymentResult, type) =>
                                               {
                                                   paymentResult.NewPaymentStatus = PaymentStatus.Paid;
                                                   paymentResult.CaptureTransactionResult = response.Ack.ToString();
                                               },
                                               (paymentResult, type) =>
                                               response.Errors.AddErrors(paymentResult.AddError),
                                               capturePaymentRequest.Order.OrderGuid);
            }

        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using (var payPalApiInterfaceClient = _payPalInterfaceService.GetService())
            {
                var response = payPalApiInterfaceClient.RefundTransaction(ref customSecurityHeaderType,
                                                                          _payPalRequestService.GetRefundTransactionRequest(refundPaymentRequest));

                return response.HandleResponse(new RefundPaymentResult(),
                                               (paymentResult, type) =>
                                               paymentResult.NewPaymentStatus = refundPaymentRequest.IsPartialRefund
                                                                                    ? PaymentStatus.PartiallyRefunded
                                                                                    : PaymentStatus.Refunded,
                                               (paymentResult, type) =>
                                               response.Errors.AddErrors(paymentResult.AddError),
                                               refundPaymentRequest.Order.OrderGuid);
            }
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();

            using (var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService())
            {
                var response = payPalApiaaInterfaceClient.DoVoid(ref customSecurityHeaderType, _payPalRequestService.GetVoidRequest(voidPaymentRequest));

                return response.HandleResponse(new VoidPaymentResult(),
                                               (paymentResult, type) =>
                                               paymentResult.NewPaymentStatus = PaymentStatus.Voided,
                                               (paymentResult, type) =>
                                               response.Errors.AddErrors(paymentResult.AddError),
                                               voidPaymentRequest.Order.OrderGuid);
            }
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            using (var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService())
            {
                var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
                CreateRecurringPaymentsProfileResponseType response =
                    payPalApiaaInterfaceClient.CreateRecurringPaymentsProfile(ref customSecurityHeaderType,
                                                                              _payPalRequestService
                                                                                  .GetCreateRecurringPaymentsProfileRequest
                                                                                  (processPaymentRequest));

                return response.HandleResponse(new ProcessPaymentResult(),
                                               (paymentResult, type) => paymentResult.NewPaymentStatus = PaymentStatus.Pending,
                                               (paymentResult, type) => response.Errors.AddErrors(paymentResult.AddError),
                                               processPaymentRequest.OrderGuid);
            }
        }


        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using (var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService())
            {

                var response =
                    payPalApiaaInterfaceClient.ManageRecurringPaymentsProfileStatus(ref customSecurityHeaderType,
                                                                                    _payPalRequestService.GetCancelRecurringPaymentRequest(cancelPaymentRequest));

                return response.HandleResponse(new CancelRecurringPaymentResult(),
                                               (paymentResult, type) => { },
                                               (paymentResult, type) => response.Errors.AddErrors(paymentResult.AddError),
                                               cancelPaymentRequest.Order.OrderGuid);
            }
        }

        public bool CanRePostProcessPayment(Order order)
        {
            return false;
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPayPalExpressCheckoutController);
        }

        public bool SupportCapture { get { return true; } }
        public bool SupportPartiallyRefund { get { return true; } }
        public bool SupportRefund { get { return true; } }
        public bool SupportVoid { get { return true; } }
        public RecurringPaymentType RecurringPaymentType { get { return RecurringPaymentType.Automatic; } }
        public PaymentMethodType PaymentMethodType { get { return PaymentMethodType.Button; } }

        public bool SkipPaymentInfo { get { return false; } }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayPalExpressCheckout";
            routeValues = new RouteValueDictionary
                              {
                                  {
                                      "Namespaces",
                                      "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers"
                                  },
                                  {"area", null}
                              };
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayPalExpressCheckout";
            routeValues = new RouteValueDictionary
                              {
                                  {
                                      "Namespaces",
                                      "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers"
                                  },
                                  {"area", null}
                              };
        }

        public override void Install()
        {
            // Setting properties
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature", "API Signature");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature.Hint", "The API Signature specified in your PayPal account");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username", "Username");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username.Hint", "The API Username specified in your PayPal account (this is not your PayPal account email)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password", "Password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password.Hint", "The API Password specified in your PayPal account (this is not your PayPal account password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive", "Live?");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive.Hint", "Check this box to make the system live (i.e. exit sandbox mode)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount", "I do not have a PayPal Business Account");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount.Hint", "I do not have a PayPal Business Account");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress", "Email Address");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress.Hint", "The email address to use if you don't have a PayPal Pro account. If you have an account, use that email, otherwise use one that you will use to create an account with to retrieve your funds");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL", "Banner Image URL");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL.Hint", "URL for the image you want to appear at the top left of the payment page. The image has a maximum size of 750 pixels wide by 90 pixels high. PayPal recommends that you provide an image that is stored on a secure (https) server. If you do not specify an image, the business name displays.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor", "Cart Border Color");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor.Hint", "The color of the cart border on the PayPal page in a 6-character HTML hexadecimal ASCII color code format");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode", "Locale Code");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode.Hint", "Locale of pages displayed by PayPal during Express Checkout");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress", "Require Confirmed Shipping Address");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress.Hint", "Indicates whether or not you require the buyer’s shipping address on file with PayPal be a confirmed address");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction", "Payment Action");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction.Hint", "Select whether you want to make a final sale, or authorise and capture at a later date (i.e. upon fulfilment)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFee", "Handling Fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFee.Hint", "Enter additional handling fee to charge your customers");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFeeIsPercentage", "Handling Fee Is Percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFeeIsPercentage.Hint", "Determines whether to apply a percentage additional handling fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging", "Enable debug logging");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging.Hint", "Allow the plugin to write extra info to the system log table");

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayPalExpressCheckoutPaymentSettings>();

            // Setting properties
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DefaultShippingPrice");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DefaultShippingPrice.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFee");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFeeIsPercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFeeIsPercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging.Hint");

            base.Uninstall();
        }

    }
}