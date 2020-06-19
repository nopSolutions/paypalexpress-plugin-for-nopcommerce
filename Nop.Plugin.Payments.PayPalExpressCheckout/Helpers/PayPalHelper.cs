using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.PayPalExpressCheckout.PayPalAPI;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Helpers
{
    public static class PayPalHelper
    {
        #region Properties

        /// <summary>
        /// Get nopCommerce partner code
        /// </summary>
        public static string BnCode => "nopCommerce_SP";

        #endregion

        #region Methods

        public static T1 HandleResponse<T1, T2>(this T2 response, T1 result, Action<T1, T2> onSuccess, Action<T1, T2> onFailure, Guid orderGuid)
        where T2 : AbstractResponseType
        {
            if (response.Ack == AckCodeType.Success || response.Ack == AckCodeType.SuccessWithWarning)
                onSuccess(result, response);
            else
                onFailure(result, response);

            LogResponse(response, orderGuid);

            return result;
        }

        public static void LogResponse<T>(this T response, Guid orderGuid)
            where T : AbstractResponseType
        {
            var chunks = GetMessage(response);

            LogOrderNotesInternal(response, orderGuid, chunks);
            LogDebugMessages(response, chunks);
        }

        public static void LogOrderNotes<T>(this T response, Guid orderGuid)
            where T : AbstractResponseType
        {
            var chunks = GetMessage(response);

            LogOrderNotesInternal(response, orderGuid, chunks);
        }

        private static void LogOrderNotesInternal<T>(T response, Guid orderGuid, List<IEnumerable<char>> chunks) where T : AbstractResponseType
        {
            var orderService = EngineContext.Current.Resolve<IOrderService>();
            var order = orderService.GetOrderByGuid(orderGuid);
            for (var index = 0; index < chunks.Count; index++)
            {
                var chunk = chunks[index];
                var message = new string(chunk.ToArray());
                var intro = $"{response.GetType().Name} returned - Part {index + 1} of {chunks.Count}";

                if (order == null)
                    continue;

                orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    DisplayToCustomer = false,
                    Note = intro + " - " + message,
                    CreatedOnUtc = DateTime.UtcNow
                });

                orderService.UpdateOrder(order);
            }
        }

        private static void LogDebugMessages<T>(T response, List<IEnumerable<char>> chunks) where T : AbstractResponseType
        {
            for (var index = 0; index < chunks.Count; index++)
            {
                var chunk = chunks[index];
                var message = new string(chunk.ToArray());
                var intro = $"{response.GetType().Name} returned - Part {index + 1} of {chunks.Count}";

                if (!EngineContext.Current.Resolve<PayPalExpressCheckoutPaymentSettings>().EnableDebugLogging)
                    continue;

                var logger = EngineContext.Current.Resolve<ILogger>();

                logger.InsertLog(LogLevel.Debug, intro, message);
            }
        }

        private static List<IEnumerable<char>> GetMessage<T>(T response) where T : AbstractResponseType
        {
            var fullMessage = JsonConvert.SerializeObject(response);
            var chunks = fullMessage.ToList().Chunk(3500).ToList();

            return chunks;
        }

        public static void AddErrors(this IEnumerable<ErrorType> errors, Action<string> addError)
        {
            foreach (var errorType in errors)
            {
                var sb = new StringBuilder()
                    .Append("LongMessage: ")
                    .Append(errorType.LongMessage)
                    .Append(Environment.NewLine)
                    .Append("ShortMessage: ")
                    .Append(errorType.ShortMessage)
                    .Append(Environment.NewLine)
                    .Append("ErrorCode: ")
                    .Append(errorType.ErrorCode)
                    .Append(Environment.NewLine);

                addError(sb.ToString());
            }
        }

        /// <summary>
        /// Break a list of items into chunks of a specific size
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IList<T> source, int chunksize)
        {
            while (source.Any())
            {
                yield return source.Take(chunksize);
                source = source.Skip(chunksize).ToList();
            }
        }

        public static BasicAmountType GetBasicAmountType(this decimal value, CurrencyCodeType currency)
        {
            var currencyService = EngineContext.Current.Resolve<ICurrencyService>();
            var workContext = EngineContext.Current.Resolve<IWorkContext>();
            var valueInCustomerCurrency = currencyService.ConvertCurrency(value, workContext.WorkingCurrency.Rate);

            return new BasicAmountType
            {
                currencyID = currency,
                Value = Math.Round(valueInCustomerCurrency, 2).ToString("N", new CultureInfo("en-us"))
            };
        }

        #endregion
    }
}