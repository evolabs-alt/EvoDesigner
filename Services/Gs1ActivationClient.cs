using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services;

public class Gs1ActivationClient
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	public async Task<Gs1ActivationResponse> ActivateAsync(string apiUrl, string bearerToken, Gs1ActivationRequest request)
	{
		if (string.IsNullOrWhiteSpace(apiUrl))
		{
			throw new InvalidOperationException("GS1 API URL is not configured.");
		}
		if (string.IsNullOrWhiteSpace(bearerToken))
		{
			throw new InvalidOperationException("GS1 API bearer token is not configured.");
		}
		using HttpClient client = new HttpClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		string content = JsonSerializer.Serialize(request, JsonOptions);
		using StringContent content2 = new StringContent(content, Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await client.PostAsync(apiUrl.Trim(), content2).ConfigureAwait(continueOnCapturedContext: false);
		string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"GS1 API returned HTTP {response.StatusCode}: {text}");
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new InvalidOperationException("GS1 API returned an empty response.");
		}
		Gs1ActivationResponse gs1ActivationResponse;
		try
		{
			gs1ActivationResponse = JsonSerializer.Deserialize<Gs1ActivationResponse>(text, JsonOptions) ?? throw new InvalidOperationException("GS1 API returned null response.");
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException("GS1 API returned non-JSON response: " + ex.Message, ex);
		}
		if (!string.Equals(gs1ActivationResponse.Result, "success", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("GS1 API returned result: " + gs1ActivationResponse.Result);
		}
		return gs1ActivationResponse;
	}
}
