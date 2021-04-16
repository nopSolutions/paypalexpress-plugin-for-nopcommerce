using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models
{
    public record PaymentInfoModel : BaseNopModel
    {
        public string ButtonImageLocation { get; set; }
    }
}