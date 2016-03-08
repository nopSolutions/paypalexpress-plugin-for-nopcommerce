using Nop.Plugin.Payments.PayPalExpressCheckout.Models;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services
{
    public interface IPayPalExpressCheckoutShippingAddressService
    {
        CheckoutShippingAddressModel PrepareShippingAddressModel(int? selectedCountryId = null);
        bool SetExistingAddress(int addressId);
        bool SetNewAddress(CheckoutShippingAddressModel checkoutShippingAddressModel);
    }
}