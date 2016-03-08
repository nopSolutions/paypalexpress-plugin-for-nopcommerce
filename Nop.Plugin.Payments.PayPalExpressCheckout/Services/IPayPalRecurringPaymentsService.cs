using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalRecurringPaymentsService
    {
        CreateRecurringPaymentsProfileRequestDetailsType GetCreateRecurringPaymentProfileRequestDetails(ProcessPaymentRequest processPaymentRequest);
    }
}