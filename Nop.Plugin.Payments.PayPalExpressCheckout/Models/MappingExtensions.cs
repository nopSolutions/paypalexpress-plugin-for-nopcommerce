using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Services.Directory;
using Nop.Services.Localization;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Models
{
    public static class MappingExtensions
    {
        /// <summary>
        /// Prepare address model
        /// </summary>
        /// <param name="model">Model</param>
        /// <param name="address">Address</param>
        /// <param name="excludeProperties">A value indicating whether to exclude properties</param>
        /// <param name="addressSettings">Address settings</param>
        /// <param name="localizationService">Localization service (used to prepare a select list)</param>
        /// <param name="stateProvinceService">State service (used to prepare a select list). null to don't prepare the list.</param>
        /// <param name="loadCountries">A function to load countries  (used to prepare a select list). null to don't prepare the list.</param>
        public static void PrepareModel(this AddressModel model,
                                        Address address, bool excludeProperties, 
                                        AddressSettings addressSettings,
                                        ILocalizationService localizationService = null,
                                        IStateProvinceService stateProvinceService = null,
                                        Func<IList<Country>> loadCountries = null)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            if (addressSettings == null)
                throw new ArgumentNullException("addressSettings");

            if (!excludeProperties && address != null)
            {
                model.Id = address.Id;
                model.FirstName = address.FirstName;
                model.LastName = address.LastName;
                model.Email = address.Email;
                model.Company = address.Company;
                model.CountryId = address.CountryId;
                model.CountryName = address.Country != null 
                                        ? address.Country.GetLocalized(x => x.Name) 
                                        : null;
                model.StateProvinceId = address.StateProvinceId;
                model.StateProvinceName = address.StateProvince != null 
                                              ? address.StateProvince.GetLocalized(x => x.Name)
                                              : null;
                model.City = address.City;
                model.Address1 = address.Address1;
                model.Address2 = address.Address2;
                model.ZipPostalCode = address.ZipPostalCode;
                model.PhoneNumber = address.PhoneNumber;
                model.FaxNumber = address.FaxNumber;
            }

            //countries and states
            if (addressSettings.CountryEnabled && loadCountries != null)
            {
                if (localizationService == null)
                    throw new ArgumentNullException("localizationService");

                model.AvailableCountries.Add(new SelectListItem() { Text = localizationService.GetResource("Address.SelectCountry"), Value = "0" });
                foreach (var c in loadCountries())
                {
                    model.AvailableCountries.Add(new SelectListItem()
                                                     {
                                                         Text = c.GetLocalized(x => x.Name),
                                                         Value = c.Id.ToString(),
                                                         Selected = c.Id == model.CountryId
                                                     });
                }

                if (addressSettings.StateProvinceEnabled)
                {
                    //states
                    if (stateProvinceService == null)
                        throw new ArgumentNullException("stateProvinceService");

                    var states = stateProvinceService
                        .GetStateProvincesByCountryId(model.CountryId.HasValue ? model.CountryId.Value : 0)
                        .ToList();
                    if (states.Count > 0)
                    {
                        foreach (var s in states)
                        {
                            model.AvailableStates.Add(new SelectListItem()
                                                          {
                                                              Text = s.GetLocalized(x => x.Name),
                                                              Value = s.Id.ToString(), 
                                                              Selected = (s.Id == model.StateProvinceId)
                                                          });
                        }
                    }
                    else
                    {
                        model.AvailableStates.Add(new SelectListItem()
                                                      {
                                                          Text = localizationService.GetResource("Address.OtherNonUS"),
                                                          Value = "0"
                                                      });
                    }
                }
            }

            //form fields
            model.CompanyEnabled = addressSettings.CompanyEnabled;
            model.CompanyRequired = addressSettings.CompanyRequired;
            model.StreetAddressEnabled = addressSettings.StreetAddressEnabled;
            model.StreetAddressRequired = addressSettings.StreetAddressRequired;
            model.StreetAddress2Enabled = addressSettings.StreetAddress2Enabled;
            model.StreetAddress2Required = addressSettings.StreetAddress2Required;
            model.ZipPostalCodeEnabled = addressSettings.ZipPostalCodeEnabled;
            model.ZipPostalCodeRequired = addressSettings.ZipPostalCodeRequired;
            model.CityEnabled = addressSettings.CityEnabled;
            model.CityRequired = addressSettings.CityRequired;
            model.CountryEnabled = addressSettings.CountryEnabled;
            model.StateProvinceEnabled = addressSettings.StateProvinceEnabled;
            model.PhoneEnabled = addressSettings.PhoneEnabled;
            model.PhoneRequired = addressSettings.PhoneRequired;
            model.FaxEnabled = addressSettings.FaxEnabled;
            model.FaxRequired = addressSettings.FaxRequired;
        }
        public static Address ToEntity(this AddressModel model)
        {
            if (model == null)
                return null;

            var entity = new Address();
            return ToEntity(model, entity);
        }
        public static Address ToEntity(this AddressModel model, Address destination)
        {
            if (model == null)
                return destination;

            destination.Id = model.Id;
            destination.FirstName = model.FirstName;
            destination.LastName = model.LastName;
            destination.Email = model.Email;
            destination.Company = model.Company;
            destination.CountryId = model.CountryId;
            destination.StateProvinceId = model.StateProvinceId;
            destination.City = model.City;
            destination.Address1 = model.Address1;
            destination.Address2 = model.Address2;
            destination.ZipPostalCode = model.ZipPostalCode;
            destination.PhoneNumber = model.PhoneNumber;
            destination.FaxNumber = model.FaxNumber;

            return destination;
        }
    }
}