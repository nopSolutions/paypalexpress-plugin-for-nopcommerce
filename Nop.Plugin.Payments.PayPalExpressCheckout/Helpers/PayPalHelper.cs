using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;

public static class PayPalHelper
{
    #region Methods

    /// <summary>
    /// Gets a payment status
    /// </summary>
    /// <param name="paymentStatus">PayPal payment status</param>
    /// <param name="pendingReason">PayPal pending reason</param>
    /// <returns>Payment status</returns>
    public static PaymentStatus GetPaymentStatus(string paymentStatus, string pendingReason)
    {
        return (paymentStatus ?? string.Empty).ToLowerInvariant() switch
        {
            "pending" when string.Equals(pendingReason, "authorization", StringComparison.InvariantCultureIgnoreCase) => PaymentStatus.Authorized,
            "processed" or "completed" or "canceled_reversal" => PaymentStatus.Paid,
            "denied" or "expired" or "failed" or "voided" => PaymentStatus.Voided,
            "refunded" or "reversed" => PaymentStatus.Refunded,
            _ => PaymentStatus.Pending
        };
    }

    #endregion
}