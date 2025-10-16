using System.Globalization;
using System.Net;
using System.Text;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayPalExpressCheckout.Helpers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services;

public class PayPalIPNService
{
    #region Fields

    private readonly ILogger _logger;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IOrderService _orderService;
    private readonly PayPalExpressCheckoutPaymentSettings _payPalExpressCheckoutPaymentSettings;
    private readonly PayPalExpressCheckoutHttpClient _payPalExpressCheckoutHttpClient;

    #endregion

    #region Ctor

    public PayPalIPNService(ILogger logger,
        IOrderProcessingService orderProcessingService,
        IOrderService orderService,
        PayPalExpressCheckoutPaymentSettings payPalExpressCheckoutPaymentSettings,
        PayPalExpressCheckoutHttpClient payPalExpressCheckoutHttpClient)
    {
        _logger = logger;
        _orderProcessingService = orderProcessingService;
        _orderService = orderService;
        _payPalExpressCheckoutPaymentSettings = payPalExpressCheckoutPaymentSettings;
        _payPalExpressCheckoutHttpClient = payPalExpressCheckoutHttpClient;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Verifies IPN
    /// </summary>
    /// <param name="formString">Form string</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result, Values
    /// </returns>
    private async Task<(bool result, Dictionary<string, string> values)> VerifyIpnAsync(string formString)
    {
        var response = string.Empty;
        try
        {
            response = await _payPalExpressCheckoutHttpClient.VerifyIpnAsync(formString, _payPalExpressCheckoutPaymentSettings);
            response = WebUtility.UrlDecode(response);
            if (string.IsNullOrEmpty(response))
                throw new NopException("No response");
        }
        catch
        {
            return default;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var success = response.Trim().Equals("VERIFIED", StringComparison.OrdinalIgnoreCase);

        foreach (var l in formString.Split('&'))
        {
            var line = l.Trim();
            var equalPox = line.IndexOf('=');
            if (equalPox >= 0)
                values.Add(line[..equalPox], line[(equalPox + 1)..]);
        }

        return (success, values);
    }

    private async Task ProcessRecurringPaymentAsync(Order order, PaymentStatus newPaymentStatus, string transactionId, string ipnInfo)
    {
        var recurringPayments = await _orderService.SearchRecurringPaymentsAsync(initialOrderId: order.Id);
        foreach (var rp in recurringPayments)
        {
            switch (newPaymentStatus)
            {
                case PaymentStatus.Authorized:
                case PaymentStatus.Paid:
                    var recurringPaymentHistory = await _orderService.GetRecurringPaymentHistoryAsync(rp);
                    if (!recurringPaymentHistory.Any())
                    {
                        await _orderService.InsertRecurringPaymentHistoryAsync(new()
                        {
                            RecurringPaymentId = rp.Id,
                            OrderId = order.Id,
                            CreatedOnUtc = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        var processPaymentResult = new ProcessPaymentResult
                        {
                            NewPaymentStatus = newPaymentStatus
                        };
                        if (newPaymentStatus == PaymentStatus.Authorized)
                            processPaymentResult.AuthorizationTransactionId = transactionId;
                        else
                            processPaymentResult.CaptureTransactionId = transactionId;

                        await _orderProcessingService.ProcessNextRecurringPaymentAsync(rp, processPaymentResult);
                    }
                    break;

                case PaymentStatus.Voided:
                    await _orderProcessingService.ProcessNextRecurringPaymentAsync(rp, new()
                    {
                        Errors = new[] { $"PayPal IPN. Recurring payment is {nameof(PaymentStatus.Voided).ToLowerInvariant()} ." },
                        RecurringPaymentFailed = true
                    });
                    break;
            }
        }

        await _logger.InformationAsync($"{Defaults.SystemName} IPN. Recurring info", new NopException(ipnInfo));
    }

    private async Task ProcessPaymentAsync(Order order, string ipnInfo, PaymentStatus newPaymentStatus, decimal mcGross, string transactionId)
    {
        await _orderService.InsertOrderNoteAsync(new()
        {
            OrderId = order.Id,
            Note = ipnInfo,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });

        //validate order total
        if ((newPaymentStatus == PaymentStatus.Authorized || newPaymentStatus == PaymentStatus.Paid) && !Math.Round(mcGross, 2).Equals(Math.Round(order.OrderTotal, 2)))
        {
            var errorStr = $"{Defaults.SystemName} IPN. Returned order total {mcGross} doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
            await _logger.ErrorAsync(errorStr);

            await _orderService.InsertOrderNoteAsync(new()
            {
                OrderId = order.Id,
                Note = errorStr,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            return;
        }

        switch (newPaymentStatus)
        {
            case PaymentStatus.Authorized:
                if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                    await _orderProcessingService.MarkAsAuthorizedAsync(order);
                break;

            case PaymentStatus.Paid:
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    order.AuthorizationTransactionId = transactionId;
                    await _orderService.UpdateOrderAsync(order);

                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                }
                break;

            case PaymentStatus.Refunded:
                var totalToRefund = Math.Abs(mcGross);
                if (totalToRefund > 0 && Math.Round(totalToRefund, 2).Equals(Math.Round(order.OrderTotal, 2)))
                {
                    //refund
                    if (_orderProcessingService.CanRefundOffline(order))
                        await _orderProcessingService.RefundOfflineAsync(order);
                }
                else
                {
                    //partial refund
                    if (_orderProcessingService.CanPartiallyRefundOffline(order, totalToRefund))
                        await _orderProcessingService.PartiallyRefundOfflineAsync(order, totalToRefund);
                }
                break;

            case PaymentStatus.Voided:
                if (_orderProcessingService.CanVoidOffline(order))
                    await _orderProcessingService.VoidOfflineAsync(order);
                break;
        }
    }

    #endregion

    #region Method

    public async Task HandleIPNAsync(string ipnData)
    {
        var (result, values) = await VerifyIpnAsync(ipnData);

        if (!result)
        {
            await _logger.ErrorAsync($"{Defaults.SystemName} IPN failed.", new NopException(ipnData));
            return;
        }

        var mcGross = values.TryGetValue("mc_gross", out var mcGrossString)
            && decimal.TryParse(mcGrossString, CultureInfo.InvariantCulture, out var mcGrossValue)
            ? mcGrossValue
            : decimal.Zero;

        values.TryGetValue("payment_status", out var paymentStatus);
        values.TryGetValue("pending_reason", out var pendingReason);
        values.TryGetValue("txn_id", out var txnId);
        values.TryGetValue("txn_type", out var txnType);
        values.TryGetValue("rp_invoice_id", out var rpInvoiceId);

        var sb = new StringBuilder();
        sb.AppendLine("Paypal IPN:");
        foreach (var kvp in values)
        {
            sb.AppendLine(kvp.Key + ": " + kvp.Value);
        }

        var newPaymentStatus = PayPalHelper.GetPaymentStatus(paymentStatus, pendingReason);
        sb.AppendLine("New payment status: " + newPaymentStatus);

        var ipnInfo = sb.ToString();

        async Task<Order> getOrderAsync(string guid)
        {
            var order = Guid.TryParse(guid, out var orderGuid) ? await _orderService.GetOrderByGuidAsync(orderGuid) : null;
            if (order is null)
                await _logger.ErrorAsync($"{Defaults.SystemName} IPN. Order is not found", new NopException(ipnInfo));

            return order;
        }

        Order order;
        switch (txnType)
        {
            case "recurring_payment":
                order = await getOrderAsync(rpInvoiceId);
                if (order is not null)
                    await ProcessRecurringPaymentAsync(order, newPaymentStatus, txnId, ipnInfo);
                break;

            case "recurring_payment_failed":
                order = await getOrderAsync(rpInvoiceId);
                if (order is null)
                    break;

                var recurringPayment = (await _orderService.SearchRecurringPaymentsAsync(initialOrderId: order.Id)).FirstOrDefault();
                if (recurringPayment is not null)
                    await _orderProcessingService.ProcessNextRecurringPaymentAsync(recurringPayment, new() { Errors = [txnType], RecurringPaymentFailed = true });
                break;

            default:
                values.TryGetValue("custom", out var orderNumber);
                order = await getOrderAsync(orderNumber);
                if (order is not null)
                    await ProcessPaymentAsync(order, ipnInfo, newPaymentStatus, mcGross, txnId);
                break;
        }
    }

    #endregion
}