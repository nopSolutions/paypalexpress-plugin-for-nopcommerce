using Autofac;
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
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<PayPalCartItemService>().AsSelf();
            builder.RegisterType<PayPalCurrencyCodeParser>().AsSelf();
            builder.RegisterType<PayPalInterfaceService>().AsSelf();
            builder.RegisterType<PayPalOrderService>().AsSelf();
            builder.RegisterType<PayPalRequestService>().AsSelf();
            builder.RegisterType<PayPalSecurityService>().AsSelf();
            builder.RegisterType<PayPalUrlService>().AsSelf();
            builder.RegisterType<PayPalCheckoutDetailsService>().AsSelf();
            builder.RegisterType<PayPalRecurringPaymentsService>().AsSelf();
            builder.RegisterType<PayPalExpressCheckoutConfirmOrderService>().AsSelf();
            builder.RegisterType<PayPalExpressCheckoutPlaceOrderService>().AsSelf();
            builder.RegisterType<PayPalExpressCheckoutService>().AsSelf();
            builder.RegisterType<PayPalExpressCheckoutShippingMethodService>().AsSelf();
            builder.RegisterType<PayPalRecurringPaymentsService>().AsSelf();
            builder.RegisterType<PayPalRedirectionService>().AsSelf();
            builder.RegisterType<PayPalIPNService>().AsSelf();
        }

        public int Order => 99;
    }
}