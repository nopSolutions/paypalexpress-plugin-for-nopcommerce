using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature")]
        public string ApiSignature { get; set; }
        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.Username")]
        public string Username { get; set; }
        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.Password")]
        public string Password { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.IsLive")]
        public bool IsLive { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.DoNotHaveBusinessAccount")]
        public bool DoNotHaveBusinessAccount { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.EmailAddress")]
        public string EmailAddress { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.LogoImageURL")]
        public string LogoImageURL { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.CartBorderColor")]
        public string CartBorderColor { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.LocaleCode")]
        public string LocaleCode { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.RequireConfirmedShippingAddress")]
        public bool RequireConfirmedShippingAddress { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.PaymentAction")]
        public PaymentActionCodeType PaymentAction { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFee")]
        public decimal HandlingFee { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.HandlingFeeIsPercentage")]
        public bool HandlingFeeIsPercentage { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging")]
        public bool EnableDebugLogging { get; set; }

        public IEnumerable<SelectListItem> PaymentActionOptions { get; set; }

        public IEnumerable<SelectListItem> LocaleOptions { get; set; }

    }
}