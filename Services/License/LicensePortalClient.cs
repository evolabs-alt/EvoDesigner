using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TaggJack.Services.License;

public sealed class LicensePortalClient
{
	private static readonly HttpClient Http = new()
	{
		Timeout = TimeSpan.FromSeconds(30)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public async Task<Dictionary<string, object?>> RequestActivationAsync(
		string hardwareKey,
		string? customerHint,
		string? contact,
		string hostname,
		string integrityDigest)
	{
		AssertAllowlistedPortal();
		var body = new Dictionary<string, object?>
		{
			["productCode"] = LicenseConstants.ProductCode,
			["hardwareKey"] = hardwareKey,
			["customerHint"] = customerHint,
			["contact"] = contact,
			["appVersion"] = LicenseConstants.AppVersion,
			["hostname"] = hostname,
			["integrityDigest"] = integrityDigest
		};
		string url = LicenseConstants.PortalBaseUrl + "/api/v1/license/activation-request";
		return await PostAsync(url, body).ConfigureAwait(false);
	}

	public async Task<Dictionary<string, object?>> ValidateAsync(string hardwareKey, string integrityDigest)
	{
		AssertAllowlistedPortal();
		var body = new Dictionary<string, object?>
		{
			["productCode"] = LicenseConstants.ProductCode,
			["hardwareKey"] = hardwareKey,
			["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			["nonce"] = Guid.NewGuid().ToString(),
			["appVersion"] = LicenseConstants.AppVersion,
			["integrityDigest"] = integrityDigest
		};
		string url = LicenseConstants.PortalBaseUrl + "/api/v1/license/validate";
		return await PostAsync(url, body).ConfigureAwait(false);
	}

	private static async Task<Dictionary<string, object?>> PostAsync(string url, Dictionary<string, object?> body)
	{
		try
		{
			string json = JsonSerializer.Serialize(body);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await Http.PostAsync(url, content).ConfigureAwait(false);
			string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			var parsed = ParseMap(text);
			parsed["httpStatus"] = (int)response.StatusCode;
			if (!parsed.ContainsKey("status") || parsed["status"] == null)
			{
				parsed["status"] = response.IsSuccessStatusCode ? "OK" : "ERROR";
			}
			return parsed;
		}
		catch (HttpRequestException ex)
		{
			return new Dictionary<string, object?>
			{
				["status"] = "UNREACHABLE",
				["reason"] = ex.Message
			};
		}
		catch (TaskCanceledException ex)
		{
			return new Dictionary<string, object?>
			{
				["status"] = "UNREACHABLE",
				["reason"] = ex.Message
			};
		}
		catch (Exception ex)
		{
			return new Dictionary<string, object?>
			{
				["status"] = "ERROR",
				["message"] = ex.Message
			};
		}
	}

	private static Dictionary<string, object?> ParseMap(string text)
	{
		var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(text))
		{
			return result;
		}
		try
		{
			using JsonDocument doc = JsonDocument.Parse(text);
			if (doc.RootElement.ValueKind != JsonValueKind.Object)
			{
				result["message"] = text;
				return result;
			}
			foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
			{
				result[prop.Name] = JsonElementToObject(prop.Value);
			}
			return result;
		}
		catch
		{
			result["message"] = text;
			return result;
		}
	}

	private static object? JsonElementToObject(JsonElement el)
	{
		return el.ValueKind switch
		{
			JsonValueKind.String => el.GetString(),
			JsonValueKind.Number => el.TryGetInt64(out long l) ? l : el.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null => null,
			_ => el.ToString()
		};
	}

	private static void AssertAllowlistedPortal()
	{
		if (!Uri.TryCreate(LicenseConstants.PortalBaseUrl, UriKind.Absolute, out Uri? uri))
		{
			throw new InvalidOperationException("Invalid portal URL");
		}
		if (!string.Equals(uri.Host, LicenseConstants.AllowlistedHost, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Portal host not allowlisted");
		}
		if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Portal must use HTTPS");
		}
	}
}
