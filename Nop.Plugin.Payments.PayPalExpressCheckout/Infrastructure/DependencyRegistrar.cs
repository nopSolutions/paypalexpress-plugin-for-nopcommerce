using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Infrastructure
{
    /// <summary>
    /// Dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="services">Collection of service descriptors</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="appSettings">App settings</param>
        public void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
        {
            services.AddScoped<PayPalCartItemService>();
            services.AddScoped<PayPalCurrencyCodeParser>();
            services.AddScoped<PayPalInterfaceService>();
            services.AddScoped<PayPalOrderService>();
            services.AddScoped<PayPalRequestService>();
            services.AddScoped<PayPalSecurityService>();
            services.AddScoped<PayPalUrlService>();
            services.AddScoped<PayPalCheckoutDetailsService>();
            services.AddScoped<PayPalRecurringPaymentsService>();
            services.AddScoped<PayPalExpressCheckoutConfirmOrderService>();
            services.AddScoped<PayPalExpressCheckoutPlaceOrderService>();
            services.AddScoped<PayPalExpressCheckoutService>();
            services.AddScoped<PayPalExpressCheckoutShippingMethodService>();
            services.AddScoped<PayPalRecurringPaymentsService>();
            services.AddScoped<PayPalRedirectionService>();
            services.AddScoped<PayPalIPNService>();
        }

        public int Order => 99;
    }
}