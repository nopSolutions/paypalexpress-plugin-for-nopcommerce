using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Orders;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PaymentPayPalExpressCheckoutController : BasePaymentController
    {
        #region Fields

        private readonly CustomerSettings _customerSettings;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly OrderSettings _orderSettings;
        private readonly PayPalExpressCheckoutConfirmOrderService _payPalExpressCheckoutConfirmOrderService;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
        private readonly PayPalExpressCheckoutPlaceOrderService _payPalExpressCheckoutPlaceOrderService;
        private readonly PayPalExpressCheckoutService _payPalExpressCheckoutService;
        private readonly PayPalExpressCheckoutShippingMethodService _payPalExpressCheckoutShippingMethodService;
        private readonly PayPalIPNService _payPalIPNService;
        private readonly PayPalRedirectionService _payPalRedirectionService;

        #endregion

        #region Ctor

        public PaymentPayPalExpressCheckoutController(CustomerSettings customerSettings,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IProductService productService,
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IWorkContext workContext,
            OrderSettings orderSettings,
            PayPalExpressCheckoutConfirmOrderService payPalExpressCheckoutConfirmOrderService,
            PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
            PayPalExpressCheckoutPlaceOrderService payPalExpressCheckoutPlaceOrderService,
            PayPalExpressCheckoutService payPalExpressCheckoutService,
            PayPalExpressCheckoutShippingMethodService payPalExpressCheckoutShippingMethodService,
            PayPalIPNService payPalIPNService,
            PayPalRedirectionService payPalRedirectionService)
        {
            _customerSettings = customerSettings;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _productService = productService;
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _workContext = workContext;
            _orderSettings = orderSettings;
            _payPalExpressCheckoutConfirmOrderService = payPalExpressCheckoutConfirmOrderService;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
            _payPalExpressCheckoutPlaceOrderService = payPalExpressCheckoutPlaceOrderService;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
            _payPalExpressCheckoutShippingMethodService = payPalExpressCheckoutShippingMethodService;
            _payPalIPNService = payPalIPNService;
            _payPalRedirectionService = payPalRedirectionService;
        }

        #endregion

        #region Utilities 

        /// <summary>
        /// Check that logo image is valid
        /// </summary>
        /// <param name="logoImageUrl">URL</param>
        /// <param name="validationErrors">Errors</param>
        /// <returns>True if logo image is valid; otherwise false</returns>
        protected bool IsLogoImageValid(string logoImageUrl, out List<string> validationErrors)
        {
            validationErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(logoImageUrl))
                return true;

            if (!Uri.TryCreate(logoImageUrl, UriKind.Absolute, out var result))
            {
                validationErrors.Add("Logo Image URL is not in a valid format");
                return false;
            }

            if (result.Scheme != Uri.UriSchemeHttps)
            {
                validationErrors.Add("Logo Image must be hosted on https");
                return false;
            }

            try
            {
                using var imageStream = WebRequest.Create(result).GetResponse().GetResponseStream();
                using var bitmap = new Bitmap(imageStream);

                if (bitmap.Width > 190)
                    validationErrors.Add("Image must be less than or equal to 190 px in width");
                if (bitmap.Height > 60)
                    validationErrors.Add("Image must be less than or equal to 60 px in height");
                return !validationErrors.Any();
            }
            catch
            {
                validationErrors.Add("Logo image was not a valid ");
                return false;
            }
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
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
                PaymentAction = _payPalExpressCheckoutPaymentSettings.PaymentAction,
                RequireConfirmedShippingAddress = _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress,
                PaymentActionOptions = _payPalExpressCheckoutService.GetPaymentActionOptions(_payPalExpressCheckoutPaymentSettings.PaymentAction),
                LocaleOptions = _payPalExpressCheckoutService.GetLocaleCodeOptions(_payPalExpressCheckoutPaymentSettings.LocaleCode)
            };

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            if (IsLogoImageValid(model.LogoImageURL, out var validationErrors))
            {
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
                _payPalExpressCheckoutPaymentSettings.PaymentAction = model.PaymentAction;
                _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress = model.RequireConfirmedShippingAddress;

                _settingService.SaveSetting(_payPalExpressCheckoutPaymentSettings);
            }
            else
            {
                foreach (var validationError in validationErrors)
                {
                    ModelState.AddModelError(string.Empty, validationError);
                }
            }

            model.PaymentActionOptions = _payPalExpressCheckoutService.GetPaymentActionOptions(model.PaymentAction);
            model.LocaleOptions = _payPalExpressCheckoutService.GetLocaleCodeOptions(model.LocaleCode);

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Configure.cshtml", model);
        }

        public ActionResult SubmitButton()
        {
            var cart = _payPalExpressCheckoutService.GetCart();

            if (!cart.Any())
                return RedirectToRoute("ShoppingCart");

            var checkoutAttributesXml = _genericAttributeService.GetAttribute<string>(_workContext.CurrentCustomer,
                NopCustomerDefaults.CheckoutAttributes, _storeContext.CurrentStore.Id);

            var scWarnings = _shoppingCartService.GetShoppingCartWarnings(cart, checkoutAttributesXml, true);
            if (scWarnings.Any())
            {
                TempData[Defaults.CheckoutErrorMessageKey] = string.Join("<br />", scWarnings);
                return RedirectToRoute("ShoppingCart");
            }

            var cartProductIds = cart.Select(ci => ci.ProductId).ToArray();

            var downloadableProductsRequireRegistration =
                _customerSettings.RequireRegistrationForDownloadableProducts && _productService.HasAnyDownloadableProduct(cartProductIds);

            if (_customerService.IsGuest(_workContext.CurrentCustomer) &&
                (!_orderSettings.AnonymousCheckoutAllowed || downloadableProductsRequireRegistration))
                return Challenge();

            return Redirect(_payPalRedirectionService.ProcessSubmitButton(cart, TempData));
        }

        public IActionResult Return(string token)
        {
            var success = _payPalRedirectionService.ProcessReturn(token);

            if (!success)
                return RedirectToRoute("ShoppingCart");

            return RedirectToAction("SetShippingMethod");
        }

        public IActionResult SetShippingMethod()
        {
            var cart = _payPalExpressCheckoutService.GetCart();

            if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
                return RedirectToAction("Confirm");

            var model = _payPalExpressCheckoutShippingMethodService.PrepareShippingMethodModel(cart);

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/SetShippingMethod.cshtml", model);
        }

        [HttpPost, ActionName("SetShippingMethod")]
        public IActionResult SetShippingMethod(string shippingoption)
        {
            //validation
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!_payPalExpressCheckoutService.IsAllowedToCheckout())
                return new UnauthorizedResult();

            if (!_shoppingCartService.ShoppingCartRequiresShipping(cart))
            {
                _payPalExpressCheckoutShippingMethodService.SetShippingMethodToNull();
                return RedirectToAction("Confirm");
            }

            var success = _payPalExpressCheckoutShippingMethodService.SetShippingMethod(cart, shippingoption);

            return RedirectToAction(success ? "Confirm" : "SetShippingMethod");
        }

        public IActionResult Confirm()
        {
            //validation
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!_payPalExpressCheckoutService.IsAllowedToCheckout())
                return new UnauthorizedResult();

            //model
            var model = _payPalExpressCheckoutConfirmOrderService.PrepareConfirmOrderModel(cart);

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Confirm.cshtml", model);
        }

        [HttpPost, ActionName("Confirm")]
        public IActionResult ConfirmOrder()
        {
            //validation
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!_payPalExpressCheckoutService.IsAllowedToCheckout())
                return new UnauthorizedResult();

            //model
            var checkoutPlaceOrderModel = _payPalExpressCheckoutPlaceOrderService.PlaceOrder();
            if (checkoutPlaceOrderModel.RedirectToCart)
                return RedirectToRoute("ShoppingCart");

            if (checkoutPlaceOrderModel.IsRedirected)
                return Content("Redirected");

            if (checkoutPlaceOrderModel.CompletedId.HasValue)
                return RedirectToRoute("CheckoutCompleted", new { orderId = checkoutPlaceOrderModel.CompletedId });

            //if we got this far, something failed, redisplay form
            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/Confirm.cshtml", checkoutPlaceOrderModel);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> IPNHandler()
        {
            using var reader = new StreamReader(Request.Body, Encoding.ASCII);

            var ipnData = await reader.ReadToEndAsync();
            _payPalIPNService.HandleIPN(ipnData);


            //nothing should be rendered to visitor
            return Content(string.Empty);
        }

        #endregion
    }
}