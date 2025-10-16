using System.Drawing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Controllers;

[AutoValidateAntiforgeryToken]
public class PaymentPayPalExpressCheckoutController : BasePaymentController
{
    #region Fields

    private readonly CustomerSettings _customerSettings;
    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IProductService _productService;
    private readonly ISettingService _settingService;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;
    private readonly OrderSettings _orderSettings;
    private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
    private readonly PayPalExpressCheckoutHttpClient _payPalExpressCheckoutHttpClient;
    private readonly PayPalExpressCheckoutService _payPalExpressCheckoutService;

    #endregion

    #region Ctor

    public PaymentPayPalExpressCheckoutController(CustomerSettings customerSettings,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IPaymentPluginManager paymentPluginManager,
        IProductService productService,
        ISettingService settingService,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IWorkContext workContext,
        OrderSettings orderSettings,
        PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
        PayPalExpressCheckoutHttpClient payPalExpressCheckoutHttpClient,
        PayPalExpressCheckoutService payPalExpressCheckoutService)
    {
        _customerSettings = customerSettings;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _paymentPluginManager = paymentPluginManager;
        _productService = productService;
        _settingService = settingService;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _workContext = workContext;
        _orderSettings = orderSettings;
        _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        _payPalExpressCheckoutHttpClient = payPalExpressCheckoutHttpClient;
        _payPalExpressCheckoutService = payPalExpressCheckoutService;
    }

    #endregion

    #region Utilities 

    /// <summary>
    /// Check that logo image is valid
    /// </summary>
    /// <param name="logoImageUrl">URL</param>
    /// <returns>True if logo image is valid; otherwise false</returns>
    private async Task<(bool Result, string Error)> IsLogoImageValidAsync(string logoImageUrl)
    {
        if (string.IsNullOrWhiteSpace(logoImageUrl))
            return (true, null);

        try
        {
            await using var imageStream = await _payPalExpressCheckoutHttpClient.GetLogoImageAsync(logoImageUrl);
            using var bitmap = new Bitmap(imageStream);

            var validationErrors = new List<string>();
            if (bitmap.Width > 190)
                validationErrors.Add("Image must be less than or equal to 190 px in width");
            if (bitmap.Height > 60)
                validationErrors.Add("Image must be less than or equal to 60 px in height");

            return (!validationErrors.Any(), string.Join('.', validationErrors));
        }
        catch
        {
            return (false, "Logo image was not a valid");
        }
    }

    private List<SelectListItem> GetLocaleCodeOptions(string localeCode)
    {
        var localeOptions = new Dictionary<string, string>
        {
            { "AU", "Australia" },
            { "AI", "Austria" },
            { "BE", "Belgium" },
            { "BR", "Brazil" },
            { "CA", "Canada" },
            { "CH", "Switzerland" },
            { "CN", "China" },
            { "DE", "Germany" },
            { "ES", "Spain" },
            { "GB", "United Kingdom" },
            { "FR", "France" },
            { "IT", "Italy" },
            { "NL", "Netherlands" },
            { "PL", "Poland" },
            { "PT", "Portugal" },
            { "RU", "Russia" },
            { "da_DK", "Danish (for Denmark only)" },
            { "he_IL", "Hebrew (all)" },
            { "id_ID", "Indonesian (for Indonesia only)" },
            { "jp_JP", "Japanese (for Japan only)" },
            { "no_NO", "Norweigan (for Norway only)" },
            { "pt_BR", "Portuguese (for Portugal and Brazil only)" },
            { "ru_RU", "Russian (for Lithuania, Latvia, and Ukraine only)" },
            { "sv_SE", "Swedish (for Sweden only)" },
            { "th_TH", "Thai (for Thailand only)" },
            { "tr_TR", "Turkish (for Turkey only)" },
            { "zh_CN", "Simplified Chinese (for China only)" },
            { "zh_HK", "Traditional Chinese (for Hong Kong only)" },
            { "zh_TW", "Traditional Chinese (for Taiwan only)" }
        }.Select(type => new SelectListItem
        {
            Selected = type.Key == localeCode,
            Text = type.Value,
            Value = type.Key
        }).OrderBy(item => item.Text).ToList();
        localeOptions.Insert(0, new SelectListItem
        {
            Text = "United States",
            Value = "US",
            Selected = localeCode == null || localeCode == "US"
        });

        return localeOptions;
    }

    #endregion

    #region Methods

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        var model = new ConfigurationModel
        {
            ApiSignature = _payPalExpressCheckoutPaymentSettings.ApiSignature,
            LogoImageURL = _payPalExpressCheckoutPaymentSettings.LogoImageURL,
            CartBorderColor = _payPalExpressCheckoutPaymentSettings.CartBorderColor,
            DoNotHaveBusinessAccount = _payPalExpressCheckoutPaymentSettings.DoNotHaveBusinessAccount,
            EmailAddress = _payPalExpressCheckoutPaymentSettings.EmailAddress,
            EnableDebugLogging = _payPalExpressCheckoutPaymentSettings.EnableDebugLogging,
            IsLive = _payPalExpressCheckoutPaymentSettings.IsLive,
            Password = _payPalExpressCheckoutPaymentSettings.Password,
            Username = _payPalExpressCheckoutPaymentSettings.Username,
            LocaleCode = _payPalExpressCheckoutPaymentSettings.LocaleCode,
            PaymentActionId = (int)_payPalExpressCheckoutService.GetPaymentAction(_payPalExpressCheckoutPaymentSettings),
            RequireConfirmedShippingAddress = _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress,
            PaymentActionOptions = _payPalExpressCheckoutService.GetPaymentActionOptions(_payPalExpressCheckoutPaymentSettings)
                .Select(item => new SelectListItem(item.Text, item.Value, item.Selected))
                .ToList(),
            LocaleOptions = GetLocaleCodeOptions(_payPalExpressCheckoutPaymentSettings.LocaleCode),
            IsActive = await _paymentPluginManager.IsPluginActiveAsync(Defaults.SystemName)
        };

        return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        var (logoImageValid, error) = await IsLogoImageValidAsync(model.LogoImageURL);
        if (!logoImageValid)
        {
            ModelState.AddModelError(string.Empty, error);
            return await Configure();
        }

        _payPalExpressCheckoutPaymentSettings.ApiSignature = model.ApiSignature;
        _payPalExpressCheckoutPaymentSettings.LogoImageURL = model.LogoImageURL;
        _payPalExpressCheckoutPaymentSettings.CartBorderColor = model.CartBorderColor;
        _payPalExpressCheckoutPaymentSettings.DoNotHaveBusinessAccount = model.DoNotHaveBusinessAccount;
        _payPalExpressCheckoutPaymentSettings.EmailAddress = model.EmailAddress;
        _payPalExpressCheckoutPaymentSettings.EnableDebugLogging = model.EnableDebugLogging;
        _payPalExpressCheckoutPaymentSettings.IsLive = model.IsLive;
        _payPalExpressCheckoutPaymentSettings.Password = model.Password;
        _payPalExpressCheckoutPaymentSettings.Username = model.Username;
        _payPalExpressCheckoutPaymentSettings.LocaleCode = model.LocaleCode;
        _payPalExpressCheckoutPaymentSettings.PaymentAction = _payPalExpressCheckoutService.GetPaymentAction(model.PaymentActionId);
        _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress = model.RequireConfirmedShippingAddress;

        await _settingService.SaveSettingAsync(_payPalExpressCheckoutPaymentSettings);

        model.PaymentActionOptions = _payPalExpressCheckoutService.GetPaymentActionOptions()
            .Select(item => new SelectListItem(item.Text, item.Value, item.Selected))
            .ToList();
        model.LocaleOptions = GetLocaleCodeOptions(model.LocaleCode);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Configure.cshtml", model);
    }

    [WwwRequirement]
    [CheckLanguageSeoCode]
    [CheckAccessPublicStore]
    [CheckAccessClosedStore]
    public async Task<ActionResult> SubmitButton()
    {
        var cart = await _payPalExpressCheckoutService.GetCartAsync();
        if (!cart.Any())
            return RedirectToRoute(NopRouteNames.General.CART);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var checkoutAttributesXml = await _genericAttributeService
            .GetAttributeAsync<string>(customer, NopCustomerDefaults.CheckoutAttributes, store.Id);

        var scWarnings = await _shoppingCartService.GetShoppingCartWarningsAsync(cart, checkoutAttributesXml, true);
        if (scWarnings.Any())
        {
            TempData[Defaults.CheckoutErrorMessageKey] = string.Join("<br />", scWarnings);
            return RedirectToRoute(NopRouteNames.General.CART);
        }

        var cartProductIds = cart.Select(ci => ci.ProductId).ToArray();
        var downloadableProductsRequireRegistration =
            _customerSettings.RequireRegistrationForDownloadableProducts && await _productService.HasAnyDownloadableProductAsync(cartProductIds);

        if (await _customerService.IsGuestAsync(customer) &&
            (!_orderSettings.AnonymousCheckoutAllowed || downloadableProductsRequireRegistration))
        {
            return Challenge();
        }

        var (url, error) = await _payPalExpressCheckoutService.ProcessSubmitButtonAsync(cart);
        if (!string.IsNullOrEmpty(error))
            TempData[Defaults.CheckoutErrorMessageKey] = error;

        return Redirect(url);
    }

    [WwwRequirement]
    [CheckLanguageSeoCode]
    [CheckAccessPublicStore]
    [CheckAccessClosedStore]
    public async Task<IActionResult> Return(string token)
    {
        var success = await _payPalExpressCheckoutService.ProcessReturnAsync(token);
        if (!success)
            return RedirectToRoute(NopRouteNames.General.CART);

        return RedirectToRoute(Defaults.Route.ShippingMethod);
    }

    [WwwRequirement]
    [CheckLanguageSeoCode]
    [CheckAccessPublicStore]
    [CheckAccessClosedStore]
    public async Task<IActionResult> SetShippingMethod()
    {
        var cart = await _payPalExpressCheckoutService.GetCartAsync();
        if (!await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart))
            return RedirectToRoute(Defaults.Route.Confirm);

        var model = await _payPalExpressCheckoutService.PrepareShippingMethodModelAsync(cart);

        return View("~/Plugins/Payments.PayPalExpressCheckout/Views/SetShippingMethod.cshtml", model);
    }

    [HttpPost, ActionName("SetShippingMethod")]
    [WwwRequirement]
    [CheckLanguageSeoCode]
    [CheckAccessPublicStore]
    [CheckAccessClosedStore]
    public async Task<IActionResult> SetShippingMethod(string shippingoption)
    {
        var cart = await _payPalExpressCheckoutService.GetCartAsync();
        if (!cart.Any())
            return RedirectToRoute(NopRouteNames.General.CART);

        if (!await _payPalExpressCheckoutService.IsAllowedToCheckoutAsync())
            return Challenge();

        if (!await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart))
        {
            await _payPalExpressCheckoutService.SetShippingMethodToNullAsync();
            return RedirectToRoute(Defaults.Route.Confirm);
        }

        var success = await _payPalExpressCheckoutService.SetShippingMethodAsync(cart, shippingoption);
        if (!success)
            return RedirectToRoute(Defaults.Route.ShippingMethod);

        return RedirectToRoute(Defaults.Route.Confirm);
    }

    [WwwRequirement]
    [CheckLanguageSeoCode]
    [CheckAccessPublicStore]
    [CheckAccessClosedStore]
    public async Task<IActionResult> Confirm()
    {
        var cart = await _payPalExpressCheckoutService.GetCartAsync();
        if (!cart.Any())
            return RedirectToRoute(NopRouteNames.General.CART);

        if (!await _payPalExpressCheckoutService.IsAllowedToCheckoutAsync())
            return Challenge();

        var model = await _payPalExpressCheckoutService.PrepareConfirmOrderModelAsync(cart);

        return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Confirm.cshtml", model);
    }

    [HttpPost, ActionName("Confirm")]
    [WwwRequirement]
    [CheckLanguageSeoCode]
    [CheckAccessPublicStore]
    [CheckAccessClosedStore]
    public async Task<IActionResult> ConfirmOrder()
    {
        var cart = await _payPalExpressCheckoutService.GetCartAsync();
        if (!cart.Any())
            return RedirectToRoute(NopRouteNames.General.CART);

        if (!await _payPalExpressCheckoutService.IsAllowedToCheckoutAsync())
            return Challenge();

        var checkoutPlaceOrderModel = await _payPalExpressCheckoutService.PlaceOrderAsync();
        if (checkoutPlaceOrderModel.RedirectToCart)
            return RedirectToRoute(NopRouteNames.General.CART);

        if (checkoutPlaceOrderModel.IsRedirected)
            return Content("Redirected");

        if (checkoutPlaceOrderModel.CompletedId.HasValue)
            return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = checkoutPlaceOrderModel.CompletedId });

        return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Confirm.cshtml", checkoutPlaceOrderModel);
    }

    #endregion
}