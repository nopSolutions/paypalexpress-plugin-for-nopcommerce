using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.PayPalExpressCheckout.Services;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Infrastructure
{
    /// <summary>
    /// Represents object for the configuring services on application startup
    /// </summary>
    public partial class PluginNopStartup : INopStartup
    {
        /// <summary>
        /// Add and configure any of the middleware
        /// </summary>
        /// <param name="services">Collection of service descriptors</param>
        /// <param name="configuration">Configuration of the application</param>
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
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

        /// <summary>
        /// Configure the using of added middleware
        /// </summary>
        /// <param name="application">Builder for configuring an application's request pipeline</param>
        public void Configure(IApplicationBuilder application)
        {
        }

        /// <summary>
        /// Gets order of this startup configuration implementation
        /// </summary>
        public int Order => 1;
    }
}