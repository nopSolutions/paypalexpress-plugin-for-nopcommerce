namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models
{
    public partial class CheckoutPlaceOrderModel : CheckoutConfirmModel
    {
        public bool RedirectToCart { get; set; }

        public bool IsRedirected { get; set; }

        public int? CompletedId { get; set; }
    }
}