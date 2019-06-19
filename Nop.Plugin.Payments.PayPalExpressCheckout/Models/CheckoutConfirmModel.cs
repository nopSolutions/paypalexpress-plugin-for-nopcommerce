using System.Collections.Generic;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models
{
    public partial class CheckoutConfirmModel : BaseNopModel
    {
        public CheckoutConfirmModel()
        {
            Warnings = new List<string>();
        }

        public string MinOrderTotalWarning { get; set; }

        public IList<string> Warnings { get; set; }
    }
}