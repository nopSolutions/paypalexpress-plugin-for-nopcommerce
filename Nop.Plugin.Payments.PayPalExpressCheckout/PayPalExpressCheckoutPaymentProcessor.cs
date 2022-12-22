using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalExpressCheckout.Components;
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var doExpressCheckoutPaymentResponseType = payPalApiaaInterfaceClient.DoExpressCheckoutPayment(ref customSecurityHeaderType,
                await _payPalRequestService.GetDoExpressCheckoutPaymentRequestAsync(processPaymentRequest));
            _session.Set(Defaults.CheckoutPaymentResponseTypeKey, doExpressCheckoutPaymentResponseType);

            return await doExpressCheckoutPaymentResponseType.HandleResponseAsync(new ProcessPaymentResult(),
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
                    paymentResult.CaptureTransactionId = paymentInfoType.TransactionID;

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
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the rue - hide; false - display.
        /// </returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the additional handling fee
        /// </returns>
        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(decimal.Zero);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the capture payment result
        /// </returns>
        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var doCaptureReq = await _payPalRequestService.GetDoCaptureRequestAsync(capturePaymentRequest);
            var response = payPalApiaaInterfaceClient.DoCapture(ref customSecurityHeaderType, doCaptureReq);

            return await response.HandleResponseAsync(new CapturePaymentResult
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiInterfaceClient = _payPalInterfaceService.GetService();
            var response = payPalApiInterfaceClient.RefundTransaction(ref customSecurityHeaderType,
                await _payPalRequestService.GetRefundTransactionRequestAsync(refundPaymentRequest));

            return await response.HandleResponseAsync(new RefundPaymentResult(),
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();

            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var response = payPalApiaaInterfaceClient.DoVoid(ref customSecurityHeaderType,
                _payPalRequestService.GetVoidRequest(voidPaymentRequest));

            return await response.HandleResponseAsync(new VoidPaymentResult(),
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public async Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            var response =
                payPalApiaaInterfaceClient.CreateRecurringPaymentsProfile(ref customSecurityHeaderType,
                    await _payPalRequestService.GetCreateRecurringPaymentsProfileRequestAsync(processPaymentRequest));

            return await response.HandleResponseAsync(new ProcessPaymentResult(),
                (paymentResult, type) => paymentResult.NewPaymentStatus = PaymentStatus.Pending,
                (paymentResult, type) => response.Errors.AddErrors(paymentResult.AddError),
                processPaymentRequest.OrderGuid);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public async Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var customSecurityHeaderType = _payPalSecurityService.GetRequesterCredentials();
            using var payPalApiaaInterfaceClient = _payPalInterfaceService.GetAAService();
            var response = payPalApiaaInterfaceClient.ManageRecurringPaymentsProfileStatus(ref customSecurityHeaderType,
                _payPalRequestService.GetCancelRecurringPaymentRequest(cancelPaymentRequest));

            return await response.HandleResponseAsync(new CancelRecurringPaymentResult(),
                (paymentResult, type) => { },
                (paymentResult, type) => response.Errors.AddErrors(paymentResult.AddError),
                cancelPaymentRequest.Order.OrderGuid);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the result
        /// </returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayPalExpressCheckout/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public Type GetPublicViewComponent()
        {
            return typeof(PaymentPayPalExpressCheckoutViewComponent);
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of validating errors
        /// </returns>
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the payment info holder
        /// </returns>
        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.PayPalExpressCheckout.PaymentMethodDescription");
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new PayPalExpressCheckoutPaymentSettings());

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
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

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<PayPalExpressCheckoutPaymentSettings>();

            // locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.PayPalExpressCheckout");

            await base.UninstallAsync();
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

        #endregion
    }
}