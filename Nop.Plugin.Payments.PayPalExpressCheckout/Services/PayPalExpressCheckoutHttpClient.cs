using System.Text;
using Microsoft.Net.Http.Headers;
using Nop.Core;

namespace Nop.Plugin.Payments.PayPalExpressCheckout.Services;

/// <summary>
/// Represents the HTTP client to request PayPal services
/// </summary>
public class PayPalExpressCheckoutHttpClient
{
    #region Fields

    private readonly HttpClient _httpClient;

    #endregion

    #region Ctor

    public PayPalExpressCheckoutHttpClient(HttpClient client)
    {
        //configure client
        client.Timeout = TimeSpan.FromSeconds(Defaults.RequestTimeout);
        client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, Defaults.UserAgent);

        _httpClient = client;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Check that logo image is valid
    /// </summary>
    /// <param name="logoImageUrl">URL</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the image stream
    /// </returns>
    public async Task<Stream> GetLogoImageAsync(string logoImageUrl)
    {
        if (!Uri.TryCreate(logoImageUrl, UriKind.Absolute, out var url))
            throw new NopException("Logo Image URL is not in a valid format");

        if (url.Scheme != Uri.UriSchemeHttps)
            throw new NopException("Logo Image must be hosted on https");

        var response = await _httpClient.GetAsync(url);
        return await response.Content.ReadAsStreamAsync();
    }

    /// <summary>
    /// Verifies IPN
    /// </summary>
    /// <param name="formString">Form string</param>
    /// <param name="settings">Plugin settings</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the IPN verification details
    /// </returns>
    public async Task<string> VerifyIpnAsync(string formString, PayPalExpressCheckoutPaymentSettings settings)
    {
        var url = settings.IsLive
            ? Defaults.ServiceUrl.Live
            : Defaults.ServiceUrl.Sandbox;
        var requestContent = new StringContent($"cmd=_notify-validate&{formString}", Encoding.UTF8, MimeTypes.ApplicationXWwwFormUrlencoded);

        //get response
        var response = await _httpClient.PostAsync(url, requestContent);

        return await response.Content.ReadAsStringAsync();
    }

    #endregion
}