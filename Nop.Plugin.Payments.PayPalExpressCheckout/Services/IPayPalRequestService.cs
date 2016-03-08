using System.Collections.Generic;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalRequestService
    {
        SetExpressCheckoutReq GetSetExpressCheckoutRequest(IList<ShoppingCartItem> shoppingCartItems);
        GetExpressCheckoutDetailsReq GetGetExpressCheckoutDetailsRequest(string token);
        DoExpressCheckoutPaymentReq GetDoExpressCheckoutPaymentRequest(ProcessPaymentRequest processPaymentRequest);
        DoCaptureReq GetDoCaptureRequest (CapturePaymentRequest capturePaymentRequest);
        RefundTransactionReq GetRefundTransactionRequest(RefundPaymentRequest refundPaymentRequest);
        DoVoidReq GetVoidRequest(VoidPaymentRequest voidPaymentRequest);
        CreateRecurringPaymentsProfileReq GetCreateRecurringPaymentsProfileRequest(
            ProcessPaymentRequest processPaymentRequest);
        ManageRecurringPaymentsProfileStatusReq GetCancelRecurringPaymentRequest(
            CancelRecurringPaymentRequest cancelRecurringPaymentRequest);
    }
}