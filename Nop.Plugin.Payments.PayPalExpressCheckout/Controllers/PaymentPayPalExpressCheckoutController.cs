using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using Nop.Plugin.Payments.PayPalExpressCheckout.Models;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;
using Nop.Services.Configuration;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Controllers
{
    public class PaymentPayPalExpressCheckoutController : BasePaymentController
    {
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPayPalExpressCheckoutService _payPalExpressCheckoutService;
        private readonly IPayPalExpressCheckoutShippingMethodService _payPalExpressCheckoutShippingMethodService;
        private readonly IPayPalExpressCheckoutConfirmOrderService _payPalExpressCheckoutConfirmOrderService;
        private readonly IPayPalExpressCheckoutPlaceOrderService _payPalExpressCheckoutPlaceOrderService;
        private readonly IPayPalRedirectionService _payPalRedirectionService;
        private readonly IPayPalIPNService _payPalIPNService;

        public PaymentPayPalExpressCheckoutController(PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
            ISettingService settingService,
            IOrderProcessingService orderProcessingService,
            IPayPalExpressCheckoutService payPalExpressCheckoutService,
            IPayPalExpressCheckoutShippingMethodService payPalExpressCheckoutShippingMethodService,
            IPayPalExpressCheckoutConfirmOrderService payPalExpressCheckoutConfirmOrderService,
            IPayPalExpressCheckoutPlaceOrderService payPalExpressCheckoutPlaceOrderService,
            IPayPalRedirectionService payPalRedirectionService,
            IPayPalIPNService payPalIPNService
            )
        {
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
            _settingService = settingService;
            _orderProcessingService = orderProcessingService;
            _payPalExpressCheckoutService = payPalExpressCheckoutService;
            _payPalExpressCheckoutShippingMethodService = payPalExpressCheckoutShippingMethodService;
            _payPalExpressCheckoutConfirmOrderService = payPalExpressCheckoutConfirmOrderService;
            _payPalExpressCheckoutPlaceOrderService = payPalExpressCheckoutPlaceOrderService;
            _payPalRedirectionService = payPalRedirectionService;
            _payPalIPNService = payPalIPNService;
        }

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();
            return warnings;
        }

        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            return paymentInfo;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            var model = new ConfigurationModel
                            {
                                ApiSignature = _payPalExpressCheckoutPaymentSettings.ApiSignature,
                                LogoImageURL = _payPalExpressCheckoutPaymentSettings.LogoImageURL,
                                CartBorderColor = _payPalExpressCheckoutPaymentSettings.CartBorderColor,
                                DoNotHaveBusinessAccount =
                                    _payPalExpressCheckoutPaymentSettings.DoNotHaveBusinessAccount,
                                EmailAddress = _payPalExpressCheckoutPaymentSettings.EmailAddress,
                                EnableDebugLogging = _payPalExpressCheckoutPaymentSettings.EnableDebugLogging,
                                IsLive = _payPalExpressCheckoutPaymentSettings.IsLive,
                                Password = _payPalExpressCheckoutPaymentSettings.Password,
                                Username = _payPalExpressCheckoutPaymentSettings.Username,
                                HandlingFee = _payPalExpressCheckoutPaymentSettings.HandlingFee,
                                HandlingFeeIsPercentage = _payPalExpressCheckoutPaymentSettings.HandlingFeeIsPercentage,
                                LocaleCode = _payPalExpressCheckoutPaymentSettings.LocaleCode,
                                PaymentAction = _payPalExpressCheckoutPaymentSettings.PaymentAction,
                                RequireConfirmedShippingAddress = _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress,
                                PaymentActionOptions = _payPalExpressCheckoutService.GetPaymentActionOptions(_payPalExpressCheckoutPaymentSettings.PaymentAction),
                                LocaleOptions = _payPalExpressCheckoutService.GetLocaleCodeOptions(_payPalExpressCheckoutPaymentSettings.LocaleCode),
                            };

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentPayPalExpressCheckout/Configure.cshtml", model);
        }
        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();
            var validationErrors = new List<string>();
            if (IsLogoImageValid(model.LogoImageURL, out validationErrors))
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
                _payPalExpressCheckoutPaymentSettings.HandlingFee = model.HandlingFee;
                _payPalExpressCheckoutPaymentSettings.HandlingFeeIsPercentage = model.HandlingFeeIsPercentage;
                _payPalExpressCheckoutPaymentSettings.LocaleCode = model.LocaleCode;
                _payPalExpressCheckoutPaymentSettings.PaymentAction = model.PaymentAction;
                _payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress =
                    model.RequireConfirmedShippingAddress;

                _settingService.SaveSetting(_payPalExpressCheckoutPaymentSettings);
            }
            else
            {
                foreach (var validationError in validationErrors)
                {
                    ModelState.AddModelError("", validationError);
                }
            }
            model.PaymentActionOptions = _payPalExpressCheckoutService.GetPaymentActionOptions(model.PaymentAction);
            model.LocaleOptions = _payPalExpressCheckoutService.GetLocaleCodeOptions(model.LocaleCode);

            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentPayPalExpressCheckout/Configure.cshtml", model);
        }

        private bool IsLogoImageValid(string logoImageUrl, out List<string> validationErrors)
        {
            validationErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(logoImageUrl)) return true;
            Uri result;
            if (!Uri.TryCreate(logoImageUrl, UriKind.Absolute, out result))
            {
                validationErrors.Add("Logo Image URL is not in a valid format");
                return false;
            }
            if (result.Scheme != "https")
            {
                validationErrors.Add("Logo Image must be hosted on https");
                return false;
            }
            try
            {
                using (var imageStream = HttpWebRequest.Create(result).GetResponse().GetResponseStream())
                using (var bitmap = new Bitmap(imageStream))
                {
                    if (bitmap.Width > 190)
                        validationErrors.Add("Image must be less than or equal to 190 px in width");
                    if (bitmap.Height > 60)
                        validationErrors.Add("Image must be less than or equal to 60 px in height");
                    return !validationErrors.Any();
                }
            }
            catch
            {
                validationErrors.Add("Logo image was not a valid ");
                return false;
            }
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return Content("");


            bool minOrderSubtotalAmountOk = _orderProcessingService.ValidateMinOrderSubtotalAmount(cart);
            if (!minOrderSubtotalAmountOk)
                return Content("");

            var model = new PaymentInfoModel()
            {
                ButtonImageLocation = "https://www.paypalobjects.com/en_GB/i/btn/btn_xpressCheckout.gif",
            };
            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentPayPalExpressCheckout/PaymentInfo.cshtml", model);
        }

        public RedirectResult SubmitButton()
        {
            var cart = _payPalExpressCheckoutService.GetCart();

            return Redirect(_payPalRedirectionService.ProcessSubmitButton(cart, TempData));
        }

        public ActionResult Return(string token)
        {
            var success = _payPalRedirectionService.ProcessReturn(token);
            return success
                       ? RedirectToAction("SetShippingMethod")
                       : RedirectToRoute("ShoppingCart");
        }

        public ActionResult SetShippingMethod()
        {
            var cart = _payPalExpressCheckoutService.GetCart();

            if (!cart.RequiresShipping())
            {
                return RedirectToAction("Confirm");
            }

            var model = _payPalExpressCheckoutShippingMethodService.PrepareShippingMethodModel(cart);
            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentPayPalExpressCheckout/SetShippingMethod.cshtml", model);
        }


        [HttpPost, ActionName("SetShippingMethod")]
        [ValidateInput(false)]
        public ActionResult SetShippingMethod(string shippingoption)
        {
            //validation
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!_payPalExpressCheckoutService.IsAllowedToCheckout())
                return new HttpUnauthorizedResult();

            if (!cart.RequiresShipping())
            {
                _payPalExpressCheckoutShippingMethodService.SetShippingMethodToNull();
                return RedirectToAction("Confirm");
            }

            var success = _payPalExpressCheckoutShippingMethodService.SetShippingMethod(cart, shippingoption);
            return RedirectToAction(success ? "Confirm" : "SetShippingMethod");
        }

        public ActionResult Confirm()
        {
            //validation
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!_payPalExpressCheckoutService.IsAllowedToCheckout())
                return new HttpUnauthorizedResult();

            //model
            var model = _payPalExpressCheckoutConfirmOrderService.PrepareConfirmOrderModel(cart);
            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentPayPalExpressCheckout/Confirm.cshtml", model);
        }

        [HttpPost, ActionName("Confirm")]
        [ValidateInput(false)]
        public ActionResult ConfirmOrder()
        {
            //validation
            var cart = _payPalExpressCheckoutService.GetCart();
            if (cart.Count == 0)
                return RedirectToRoute("ShoppingCart");

            if (!_payPalExpressCheckoutService.IsAllowedToCheckout())
                return new HttpUnauthorizedResult();

            //model
            var checkoutPlaceOrderModel = _payPalExpressCheckoutPlaceOrderService.PlaceOrder();
            if (checkoutPlaceOrderModel.RedirectToCart)
                return RedirectToRoute("ShoppingCart");
            if (checkoutPlaceOrderModel.IsRedirected)
                return Content("Redirected");
            if (checkoutPlaceOrderModel.CompletedId.HasValue)
                return RedirectToRoute("CheckoutCompleted", new { orderId = checkoutPlaceOrderModel.CompletedId });

            //If we got this far, something failed, redisplay form
            return View("~/Plugins/Payments.PayPalExpressCheckout/Views/PaymentPayPalExpressCheckout/Confirm.cshtml", checkoutPlaceOrderModel);
        }

        [ValidateInput(false)]
        public ActionResult IPNHandler()
        {
            byte[] param = Request.BinaryRead(Request.ContentLength);
            string ipnData = Encoding.ASCII.GetString(param);

            _payPalIPNService.HandleIPN(ipnData);

            //nothing should be rendered to visitor
            return Content("");
        }
    }
}