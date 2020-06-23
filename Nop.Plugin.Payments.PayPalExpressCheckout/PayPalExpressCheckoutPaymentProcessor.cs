using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.PayPalExpressCheckout
{
    public class PayPalExpressCheckoutPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ISession _session;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
        private readonly PayPalInterfaceService _payPalInterfaceService;
        private readonly PayPalRequestService _payPalRequestService;
        private readonly PayPalSecurityService _payPalSecurityService;

        #endregion

        #region Ctor

        public PayPalExpressCheckoutPaymentProcessor(IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            ISettingService settingService,
            IWebHelper webHelper,
            PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
            PayPalInterfaceService payPalInterfaceService,
            PayPalRequestService payPalRequestService,
            PayPalSecurityService payPalSecurityService)
        {
            _session = httpContextAccessor.HttpContext?.Session;
            _localizationService = localizationService;
            _settingService = settingService;
            _webHelper = webHelper;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
            _payPalInterfaceService = payPalInterfaceService;
            _payPalRequestService = payPalRequestService;
            _payPalSecurityService = payPalSecurityService;
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
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var doExpressCheckoutPaymentResponseType = payPalApiaaInterfaceClient.DoExpressCheckoutPayment(ref customSecurityHeaderType,
                _payPalRequestService.GetDoExpressCheckoutPaymentRequest(processPaymentRequest));
            _session.Set(Defaults.CheckoutPaymentResponseTypeKey, doExpressCheckoutPaymentResponseType);

            return doExpressCheckoutPaymentResponseType.HandleResponse(new ProcessPaymentResult(),
            (paymentResult, type) =>
            {
                paymentResult.NewPaymentStatus =
                _payPalExpressCheckoutPaymentSettings.PaymentAction == PaymentActionCodeType.Authorization
                       ? PaymentStatus.Authorized
                       : PaymentStatus.Paid;

                paymentResult.AuthorizationTransactionId =
                processPaymentRequest.CustomValues[Defaults.PaypalTokenKey].ToString();
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
            return decimal.Zero;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
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

                    if (type.DoCaptureResponseDetails.PaymentInfo is PaymentInfoType pInfoType)
                        paymentResult.CaptureTransactionId = pInfoType.TransactionID;
                },
                (paymentResult, type) =>
                    response.Errors.AddErrors(paymentResult.AddError),
                capturePaymentRequest.Order.OrderGuid);
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiInterfaceClient = _payPalInterfaceService.GetService();
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

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();

            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var response = payPalApiaaInterfaceClient.DoVoid(ref customSecurityHeaderType,
                _payPalRequestService.GetVoidRequest(voidPaymentRequest));

            return response.HandleResponse(new VoidPaymentResult(),
                (paymentResult, type) =>
                    paymentResult.NewPaymentStatus = PaymentStatus.Voided,
                (paymentResult, type) =>
                    response.Errors.AddErrors(paymentResult.AddError),
                voidPaymentRequest.Order.OrderGuid);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            var response =
                payPalApiaaInterfaceClient.CreateRecurringPaymentsProfile(ref customSecurityHeaderType,
                    _payPalRequestService.GetCreateRecurringPaymentsProfileRequest(processPaymentRequest));

            return response.HandleResponse(new ProcessPaymentResult(),
                (paymentResult, type) => paymentResult.NewPaymentStatus = PaymentStatus.Pending,
                (paymentResult, type) => response.Errors.AddErrors(paymentResult.AddError),
                processPaymentRequest.OrderGuid);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var response = payPalApiaaInterfaceClient.ManageRecurringPaymentsProfileStatus(ref customSecurityHeaderType,
                _payPalRequestService.GetCancelRecurringPaymentRequest(cancelPaymentRequest));

            return response.HandleResponse(new CancelRecurringPaymentResult(),
                (paymentResult, type) => { },
                (paymentResult, type) => response.Errors.AddErrors(paymentResult.AddError),
                cancelPaymentRequest.Order.OrderGuid);
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

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayPalExpressCheckout/Configure";
        }

        public string GetPublicViewComponentName()
        {
            return "PaymentPayPalExpressCheckout";
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new PayPalExpressCheckoutPaymentSettings());

            //locales
            _localizationService.AddPluginLocaleResource(new Dictionary<string, string>
            {
                ["Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature"] = "API Signature",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature.Hint"] = "The API Signature specified in your PayPal account.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor"] = "Cart Border Color",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor.Hint"] = "The color of the cart border on the PayPal page in a 6-character HTML hexadecimal ASCII color code format.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount"] = "I do not have a PayPal Business Account",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount.Hint"] = "I do not have a PayPal Business Account.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress"] = "Email Address",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress.Hint"] = "The email address to use if you don't have a PayPal Pro account. If you have an account, use that email, otherwise use one that you will use to create an account with to retrieve your funds.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging"] = "Enable debug logging",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging.Hint"] = "Allow the plugin to write extra info to the system log table.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.IsLive"] = "Live?",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.IsLive.Hint"] = "Check this box to make the system live (i.e. exit sandbox mode).",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode"] = "Locale Code",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode.Hint"] = "Locale of pages displayed by PayPal during Express Checkout.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL"] = "Banner Image URL",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL.Hint"] = "URL for the image you want to appear at the top left of the payment page. The image has a maximum size of 750 pixels wide by 90 pixels high. PayPal recommends that you provide an image that is stored on a secure (https) server. If you do not specify an image, the business name displays.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.Password"] = "Password",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.Password.Hint"] = "The API Password specified in your PayPal account (this is not your PayPal account password).",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction"] = "Payment Action",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction.Hint"] = "Select whether you want to make a final sale, or authorise and capture at a later date (i.e. upon fulfilment).",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress"] = "Require Confirmed Shipping Address",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress.Hint"] = "Indicates whether or not you require the buyer’s shipping address on file with PayPal be a confirmed address.",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.Username"] = "Username",
                ["Plugins.Payments.PayPalExpressCheckout.Fields.Username.Hint"] = "The API Username specified in your PayPal account (this is not your PayPal account email)",
                ["Plugins.Payments.PayPalExpressCheckout.PaymentMethodDescription"] = "Pay by PayPal"
            });

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
            _localizationService.DeletePluginLocaleResources("Plugins.Payments.PayPalExpressCheckout");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => true;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => true;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => true;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => true;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Button;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.PayPalExpressCheckout.PaymentMethodDescription");

        #endregion
    }
}