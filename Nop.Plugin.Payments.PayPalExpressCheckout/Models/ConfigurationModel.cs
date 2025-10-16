using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.ApiSignature")]
    public string ApiSignature { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.Username")]
    public string Username { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.Password")]
    [NoTrim]
    [DataType(DataType.Password)]
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
    public int PaymentActionId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.PayPalExpressCheckout.Fields.EnableDebugLogging")]
    public bool EnableDebugLogging { get; set; }

    public List<SelectListItem> PaymentActionOptions { get; set; } = new();

    public List<SelectListItem> LocaleOptions { get; set; } = new();

    public bool IsActive { get; set; }
}