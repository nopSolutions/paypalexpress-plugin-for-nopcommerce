using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http.Extensions;
using Nop.Plugin.Payments.PayPalExpressCheckout.Components;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PayPalExpressCheckout;

public class PayPalExpressCheckoutPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalizationService _localizationService;
    private readonly INopUrlHelper _nopUrlHelper;
    private readonly ISettingService _settingService;
    private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
    private readonly PayPalExpressCheckoutService _payPalExpressCheckoutService;

    #endregion

    #region Ctor

    public PayPalExpressCheckoutPaymentProcessor(IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        INopUrlHelper nopUrlHelper,
        ISettingService settingService,
        PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
        PayPalExpressCheckoutService payPalExpressCheckoutService)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
        _nopUrlHelper = nopUrlHelper;
        _settingService = settingService;
        _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        _payPalExpressCheckoutService = payPalExpressCheckoutService;
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
        var credentials = _payPalExpressCheckoutService.GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = _payPalExpressCheckoutService.GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = await _payPalExpressCheckoutService.GetDoExpressCheckoutPaymentRequestAsync(processPaymentRequest);
        var response = client.DoExpressCheckoutPayment(ref credentials, request);

        await _httpContextAccessor.HttpContext.Session.SetAsync(Defaults.CheckoutPaymentResponseTypeKey, response);

        return await _payPalExpressCheckoutService.HandleResponseAsync(_payPalExpressCheckoutPaymentSettings,
            response, new ProcessPaymentResult(),
            onSuccess: (paymentResult, type) =>
            {
                paymentResult.NewPaymentStatus = _payPalExpressCheckoutService.GetStatusByPaymentAction(_payPalExpressCheckoutPaymentSettings);
                if (string.Equals(_payPalExpressCheckoutPaymentSettings.PaymentAction, "Authorization", StringComparison.InvariantCultureIgnoreCase))
                    paymentResult.AuthorizationTransactionId = type.DoExpressCheckoutPaymentResponseDetails.PaymentInfo?.FirstOrDefault()?.TransactionID;
                else
                    paymentResult.CaptureTransactionId = type.DoExpressCheckoutPaymentResponseDetails.PaymentInfo?.FirstOrDefault()?.TransactionID;

                paymentResult.CaptureTransactionResult = type.Ack.ToString();
            },
            onFailure: (paymentResult, type) =>
            {
                paymentResult.NewPaymentStatus = PaymentStatus.Pending;
                _payPalExpressCheckoutService.AddErrors(type.Errors, paymentResult.AddError);
                paymentResult.AddError(type.DoExpressCheckoutPaymentResponseDetails.RedirectRequired);
            });
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
        var credentials = _payPalExpressCheckoutService.GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = _payPalExpressCheckoutService.GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = await _payPalExpressCheckoutService.GetDoCaptureRequestAsync(capturePaymentRequest);
        var response = client.DoCapture(ref credentials, request);

        return await _payPalExpressCheckoutService.HandleResponseAsync(_payPalExpressCheckoutPaymentSettings,
            response, new CapturePaymentResult(),
            onSuccess: (paymentResult, type) =>
            {
                paymentResult.NewPaymentStatus = PaymentStatus.Paid;
                paymentResult.CaptureTransactionResult = response.Ack.ToString();
                paymentResult.CaptureTransactionId = response.DoCaptureResponseDetails?.PaymentInfo?.TransactionID
                    ?? capturePaymentRequest.Order.AuthorizationTransactionId;

            },
            onFailure: (paymentResult, type) => _payPalExpressCheckoutService.AddErrors(response.Errors, paymentResult.AddError),
            order: capturePaymentRequest.Order);
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
        var credentials = _payPalExpressCheckoutService.GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = _payPalExpressCheckoutService.GetClient(_payPalExpressCheckoutPaymentSettings);
        var request = await _payPalExpressCheckoutService.GetRefundTransactionRequestAsync(refundPaymentRequest);
        var response = client.RefundTransaction(ref credentials, request);

        return await _payPalExpressCheckoutService.HandleResponseAsync(_payPalExpressCheckoutPaymentSettings,
            response, new RefundPaymentResult(),
            onSuccess: (paymentResult, type) => paymentResult.NewPaymentStatus = refundPaymentRequest.IsPartialRefund
                ? PaymentStatus.PartiallyRefunded
                : PaymentStatus.Refunded,
            onFailure: (paymentResult, type) => _payPalExpressCheckoutService.AddErrors(response.Errors, paymentResult.AddError),
            order: refundPaymentRequest.Order);
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
        var credentials = _payPalExpressCheckoutService.GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = _payPalExpressCheckoutService.GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = _payPalExpressCheckoutService.GetVoidRequest(voidPaymentRequest);
        var response = client.DoVoid(ref credentials, request);

        return await _payPalExpressCheckoutService.HandleResponseAsync(_payPalExpressCheckoutPaymentSettings,
            response, new VoidPaymentResult(),
            onSuccess: (paymentResult, type) => paymentResult.NewPaymentStatus = PaymentStatus.Voided,
            onFailure: (paymentResult, type) => _payPalExpressCheckoutService.AddErrors(response.Errors, paymentResult.AddError),
            order: voidPaymentRequest.Order);
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
        var credentials = _payPalExpressCheckoutService.GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = _payPalExpressCheckoutService.GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = await _payPalExpressCheckoutService.GetCreateRecurringPaymentsProfileRequestAsync(processPaymentRequest);
        var response = client.CreateRecurringPaymentsProfile(ref credentials, request);

        return await _payPalExpressCheckoutService.HandleResponseAsync(_payPalExpressCheckoutPaymentSettings,
            response, new ProcessPaymentResult(),
            onSuccess: (paymentResult, type) => paymentResult.NewPaymentStatus = PaymentStatus.Pending,
            onFailure: (paymentResult, type) => _payPalExpressCheckoutService.AddErrors(response.Errors, paymentResult.AddError));
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
        var credentials = _payPalExpressCheckoutService.GetCredentials(_payPalExpressCheckoutPaymentSettings);
        using var client = _payPalExpressCheckoutService.GetAAClient(_payPalExpressCheckoutPaymentSettings);
        var request = _payPalExpressCheckoutService.GetCancelRecurringPaymentRequest(cancelPaymentRequest);
        var response = client.ManageRecurringPaymentsProfileStatus(ref credentials, request);

        return await _payPalExpressCheckoutService.HandleResponseAsync(_payPalExpressCheckoutPaymentSettings,
            response, new CancelRecurringPaymentResult(),
            onSuccess: (paymentResult, type) => { },
            onFailure: (paymentResult, type) => _payPalExpressCheckoutService.AddErrors(response.Errors, paymentResult.AddError),
            order: cancelPaymentRequest.Order);
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
        return _nopUrlHelper.RouteUrl(Defaults.Route.Configuration);
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