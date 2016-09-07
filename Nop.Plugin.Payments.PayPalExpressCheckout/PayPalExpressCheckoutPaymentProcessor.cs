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
        #region Fields

        private readonly HttpSessionStateBase _session;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPayPalInterfaceService _payPalInterfaceService;
        private readonly IPayPalRequestService _payPalRequestService;
        private readonly IPayPalSecurityService _payPalSecurityService;        
        private readonly ISettingService _settingService;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        #endregion

        #region Ctor

        public PayPalExpressCheckoutPaymentProcessor(HttpSessionStateBase session,
            IOrderTotalCalculationService orderTotalCalculationService,
            IPayPalInterfaceService payPalInterfaceService,
            IPayPalRequestService payPalRequestService,
            IPayPalSecurityService payPalSecurityService,        
            ISettingService settingService,
            PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _session = session;
            _orderTotalCalculationService = orderTotalCalculationService;
            _payPalInterfaceService = payPalInterfaceService;
            _payPalRequestService = payPalRequestService;
            _payPalSecurityService = payPalSecurityService;            
            _settingService = settingService;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
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

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
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

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _payPalExpressCheckoutPaymentSettings.AdditionalFee, _payPalExpressCheckoutPaymentSettings.AdditionalFeePercentage);

            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
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

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
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

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
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

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
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


        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
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

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            return false;
        }

        /// <summary>
        /// Get type of the controller
        /// </summary>
        /// <returns>Controller type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentPayPalExpressCheckoutController);
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayPalExpressCheckout";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayPalExpressCheckout";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.PayPalExpressCheckout.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new PayPalExpressCheckoutPaymentSettings());

            // locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature", "API Signature");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature.Hint", "The API Signature specified in your PayPal account.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor", "Cart Border Color");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor.Hint", "The color of the cart border on the PayPal page in a 6-character HTML hexadecimal ASCII color code format.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount", "I do not have a PayPal Business Account");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount.Hint", "I do not have a PayPal Business Account.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress", "Email Address");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress.Hint", "The email address to use if you don't have a PayPal Pro account. If you have an account, use that email, otherwise use one that you will use to create an account with to retrieve your funds.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging", "Enable debug logging");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging.Hint", "Allow the plugin to write extra info to the system log table.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive", "Live?");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive.Hint", "Check this box to make the system live (i.e. exit sandbox mode).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode", "Locale Code");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode.Hint", "Locale of pages displayed by PayPal during Express Checkout.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL", "Banner Image URL");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL.Hint", "URL for the image you want to appear at the top left of the payment page. The image has a maximum size of 750 pixels wide by 90 pixels high. PayPal recommends that you provide an image that is stored on a secure (https) server. If you do not specify an image, the business name displays.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password", "Password");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password.Hint", "The API Password specified in your PayPal account (this is not your PayPal account password).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction", "Payment Action");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction.Hint", "Select whether you want to make a final sale, or authorise and capture at a later date (i.e. upon fulfilment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress", "Require Confirmed Shipping Address");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress.Hint", "Indicates whether or not you require the buyer’s shipping address on file with PayPal be a confirmed address.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username", "Username");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username.Hint", "The API Username specified in your PayPal account (this is not your PayPal account email)");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayPalExpressCheckoutPaymentSettings>();

            // locales
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Password.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username");
            this.DeletePluginLocaleResource("Plugins.Payments.PayPalExpressCheckout.Fields.Username.Hint");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Button; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        #endregion
    }
}