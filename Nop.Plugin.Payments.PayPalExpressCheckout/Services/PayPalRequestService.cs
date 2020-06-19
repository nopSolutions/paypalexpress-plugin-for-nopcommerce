using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Catalog;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public class PayPalRequestService : IPayPalRequestService
    {
        private readonly IPayPalUrlService _payPalUrlService;
        private readonly IPayPalOrderService _payPalOrderService;
        private readonly IPayPalCurrencyCodeParser _payPalCurrencyCodeParser;
        private readonly IPayPalRecurringPaymentsService _payPalRecurringPaymentsService;
        private readonly IProductService _productService;
        private readonly IWorkContext _workContext;
        private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;

        public PayPalRequestService(IPayPalUrlService payPalUrlService,
            IPayPalOrderService payPalOrderService,
            IPayPalCurrencyCodeParser payPalCurrencyCodeParser,
            IPayPalRecurringPaymentsService payPalRecurringPaymentsService,
            IProductService productService,
            IWorkContext workContext,
            PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings)
        {
            _payPalUrlService = payPalUrlService;
            _payPalOrderService = payPalOrderService;
            _payPalCurrencyCodeParser = payPalCurrencyCodeParser;
            _payPalRecurringPaymentsService = payPalRecurringPaymentsService;
            _productService = productService;
            _workContext = workContext;
            _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        }

        public SetExpressCheckoutRequestDetailsType GetSetExpressCheckoutRequestDetails(IList<ShoppingCartItem> cart)
        {
            var noShippingCart = cart.Any(item => _productService.GetProductById(item.ProductId)?.IsDownload != true);

            var setExpressCheckoutRequestDetailsType =
                new SetExpressCheckoutRequestDetailsType
                {
                    ReturnURL = _payPalUrlService.GetReturnURL(),
                    CancelURL = _payPalUrlService.GetCancelURL(),
                    ReqConfirmShipping = noShippingCart || !_payPalExpressCheckoutPaymentSettings.RequireConfirmedShippingAddress ? "0" : "1",
                    NoShipping = noShippingCart ? "2" : "1",
                    LocaleCode = _payPalExpressCheckoutPaymentSettings.LocaleCode,
                    cppheaderimage = _payPalExpressCheckoutPaymentSettings.LogoImageURL,
                    cppcartbordercolor = _payPalExpressCheckoutPaymentSettings.CartBorderColor,
                    PaymentDetails = _payPalOrderService.GetPaymentDetails(cart),
                    BuyerEmail = _payPalOrderService.GetBuyerEmail(),
                    MaxAmount = _payPalOrderService.GetMaxAmount(cart)
                };

            return setExpressCheckoutRequestDetailsType;
        }

        public SetExpressCheckoutReq GetSetExpressCheckoutRequest(IList<ShoppingCartItem> shoppingCartItems)
        {
            var setExpressCheckoutRequestDetailsType = GetSetExpressCheckoutRequestDetails(shoppingCartItems);
            var setExpressCheckoutRequestType = new SetExpressCheckoutRequestType
            {
                SetExpressCheckoutRequestDetails = setExpressCheckoutRequestDetailsType,
                Version = GetVersion()
            };

            return new SetExpressCheckoutReq { SetExpressCheckoutRequest = setExpressCheckoutRequestType };
        }

        public string GetVersion()
        {
            return "98.0";
        }

        public GetExpressCheckoutDetailsReq GetGetExpressCheckoutDetailsRequest(string token)
        {
            return new GetExpressCheckoutDetailsReq
            {
                GetExpressCheckoutDetailsRequest = new GetExpressCheckoutDetailsRequestType
                {
                    Token = token,
                    Version = GetVersion()
                }
            };
        }

        public DoExpressCheckoutPaymentReq GetDoExpressCheckoutPaymentRequest(ProcessPaymentRequest processPaymentRequest)
        {
            // populate payment details
            var currencyCodeType = _payPalCurrencyCodeParser.GetCurrencyCodeType(_workContext.WorkingCurrency);

            var paymentDetails = new PaymentDetailsType
            {
                OrderTotal = processPaymentRequest.OrderTotal.GetBasicAmountType(currencyCodeType),
                Custom = processPaymentRequest.OrderGuid.ToString(),
                ButtonSource = PayPalHelper.BnCode,
                InvoiceID = processPaymentRequest.OrderGuid.ToString()
            };

            // build the request
            return new DoExpressCheckoutPaymentReq
            {
                DoExpressCheckoutPaymentRequest = new DoExpressCheckoutPaymentRequestType
                {
                    Version = GetVersion(),
                    DoExpressCheckoutPaymentRequestDetails = new DoExpressCheckoutPaymentRequestDetailsType
                    {
                        Token = processPaymentRequest.CustomValues[Defaults.PaypalTokenKey].ToString(),
                        PayerID = processPaymentRequest.CustomValues[Defaults.PaypalPayerIdKey].ToString(),
                        PaymentAction = _payPalExpressCheckoutPaymentSettings.PaymentAction,
                        PaymentActionSpecified = true,
                        ButtonSource = PayPalHelper.BnCode,
                        PaymentDetails = new[] { paymentDetails }
                    }
                }
            };
        }

        public DoCaptureReq GetDoCaptureRequest(CapturePaymentRequest capturePaymentRequest)
        {
            var currencyCodeType =
                _payPalCurrencyCodeParser.GetCurrencyCodeType(capturePaymentRequest.Order.CustomerCurrencyCode);
            return new DoCaptureReq
            {
                DoCaptureRequest = new DoCaptureRequestType
                {
                    Amount = capturePaymentRequest.Order.OrderTotal.GetBasicAmountType(currencyCodeType),
                    AuthorizationID = capturePaymentRequest.Order.CaptureTransactionId,
                    CompleteType = CompleteCodeType.Complete,
                    InvoiceID = capturePaymentRequest.Order.OrderGuid.ToString(),
                    Version = GetVersion(),
                    MsgSubID = capturePaymentRequest.Order.Id + "-capture"
                }
            };
        }

        public RefundTransactionReq GetRefundTransactionRequest(RefundPaymentRequest refundPaymentRequest)
        {
            var transactionId = refundPaymentRequest.Order.CaptureTransactionId;
            var refundType = refundPaymentRequest.IsPartialRefund ? RefundType.Partial : RefundType.Full;

            var currencyCodeType = _payPalCurrencyCodeParser.GetCurrencyCodeType(refundPaymentRequest.Order.CustomerCurrencyCode);
            return new RefundTransactionReq
            {
                RefundTransactionRequest = new RefundTransactionRequestType
                {
                    RefundType = refundType,
                    RefundTypeSpecified = true,
                    Version = GetVersion(),
                    TransactionID = transactionId,
                    Amount = refundPaymentRequest.AmountToRefund.GetBasicAmountType(currencyCodeType),
                    MsgSubID = $"{refundPaymentRequest.Order.Id}-{refundPaymentRequest.IsPartialRefund}-{refundPaymentRequest.AmountToRefund:0.00}"
                }
            };
        }

        public DoVoidReq GetVoidRequest(VoidPaymentRequest voidPaymentRequest)
        {
            var transactionId = voidPaymentRequest.Order.CaptureTransactionId;
            if (string.IsNullOrEmpty(transactionId))
                transactionId = voidPaymentRequest.Order.AuthorizationTransactionId;

            return new DoVoidReq
            {
                DoVoidRequest = new DoVoidRequestType
                {
                    Version = GetVersion(),
                    AuthorizationID = transactionId,
                    MsgSubID = voidPaymentRequest.Order.Id + "-void"
                }
            };
        }

        public CreateRecurringPaymentsProfileReq GetCreateRecurringPaymentsProfileRequest(
            ProcessPaymentRequest processPaymentRequest)
        {
            var req = new CreateRecurringPaymentsProfileReq
            {
                CreateRecurringPaymentsProfileRequest = new CreateRecurringPaymentsProfileRequestType
                {
                    Version = GetVersion(),
                    CreateRecurringPaymentsProfileRequestDetails = _payPalRecurringPaymentsService.GetCreateRecurringPaymentProfileRequestDetails(processPaymentRequest)
                }
            };

            return req;
        }

        public ManageRecurringPaymentsProfileStatusReq GetCancelRecurringPaymentRequest(
            CancelRecurringPaymentRequest cancelRecurringPaymentRequest)
        {
            return new ManageRecurringPaymentsProfileStatusReq
            {
                ManageRecurringPaymentsProfileStatusRequest = new ManageRecurringPaymentsProfileStatusRequestType
                {
                    Version = GetVersion(),
                    ManageRecurringPaymentsProfileStatusRequestDetails = new ManageRecurringPaymentsProfileStatusRequestDetailsType
                    {
                        Action = StatusChangeActionType.Cancel,
                        ProfileID = cancelRecurringPaymentRequest.Order.OrderGuid.ToString()
                    }
                }
            };
        }
    }
}