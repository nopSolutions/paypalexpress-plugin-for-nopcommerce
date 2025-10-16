using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models;

public record CheckoutShippingAddressModel : BaseNopModel
{
    public CheckoutShippingAddressModel()
    {
        ExistingAddresses = new List<AddressModel>();
        NewAddress = new AddressModel();
    }

    public IList<AddressModel> ExistingAddresses { get; set; }

    public AddressModel NewAddress { get; set; }

    public bool NewAddressPreselected { get; set; }
}