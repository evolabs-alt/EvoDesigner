using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services.License;

/// <summary>Portal-backed license entitlement for TAGGJACK.</summary>
public sealed class PortalLicenseService
{
	private static readonly Lazy<PortalLicenseService> LazyInstance = new(() => new PortalLicenseService());

	public static PortalLicenseService Instance => LazyInstance.Value;

	private readonly LicensePortalClient _portal = new();
	private readonly LicenseLeaseVerifier _verifier = new();
	private readonly LicenseLeaseStore _store = new();
	private readonly LicenseIntegrityService _integrity = new();

	private PortalLicenseService()
	{
	}

	public string GetHardwareKey() => HardwareIdService.GetHardwareKey();

	public async Task<Dictionary<string, object?>> RequestActivationAsync(string? customerHint, string? contact)
	{
		string hw = GetHardwareKey();
		string hostname;
		try
		{
			hostname = Dns.GetHostName();
		}
		catch
		{
			hostname = "unknown";
		}
		var result = await _portal.RequestActivationAsync(
			hw, customerHint, contact, hostname, _integrity.GetIntegrityDigest()).ConfigureAwait(false);
		if (string.Equals(Str(result, "status"), "PENDING", StringComparison.OrdinalIgnoreCase))
		{
			_store.SetPending("Waiting for Evolabs approval");
		}
		return result;
	}

	public async Task<Dictionary<string, object?>> CheckWithPortalAsync()
	{
		string hw = GetHardwareKey();
		var result = await _portal.ValidateAsync(hw, _integrity.GetIntegrityDigest()).ConfigureAwait(false);
		string status = Str(result, "status") ?? "";

		if (string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
		    result.TryGetValue("leaseJwt", out object? jwtObj) &&
		    jwtObj is string leaseJwt &&
		    !string.IsNullOrWhiteSpace(leaseJwt))
		{
			try
			{
				_verifier.Verify(leaseJwt, hw);
				_store.Update(new LicenseLeaseState
				{
					LeaseJwt = leaseJwt,
					HardwareKey = hw,
					LastSuccessAt = DateTimeOffset.UtcNow,
					Status = "ACTIVE",
					Message = "Licensed",
					LicenseExpiresAt = Str(result, "expiresAt"),
					CustomerName = Str(result, "customerName")
				});
			}
			catch (Exception ex)
			{
				_store.SetInvalid("Invalid lease signature from portal: " + ex.Message);
				result["status"] = "INVALID";
				result["reason"] = "bad_lease_signature";
			}
		}
		else if (string.Equals(status, "UNREACHABLE", StringComparison.OrdinalIgnoreCase))
		{
			// keep cache; EvaluateLocal applies grace
		}
		else
		{
			_store.SetInvalid(Str(result, "reason") ?? status);
		}
		return result;
	}

	public bool IsOperationAllowed()
	{
		if (!LicenseConstants.EnforcementEnabled)
		{
			return true;
		}
		return EvaluateLocal().Allowed;
	}

	public void RequireLicensed()
	{
		if (!IsOperationAllowed())
		{
			throw new LicenseDeniedException("TaggJack license is not active for this machine");
		}
	}

	public LicenseEvaluation GetStatus()
	{
		LicenseEvaluation status = EvaluateLocal();
		status.HardwareKey = GetHardwareKey();
		status.ProductCode = LicenseConstants.ProductCode;
		status.Portal = LicenseConstants.PortalBaseUrl;
		status.AppVersion = LicenseConstants.AppVersion;
		return status;
	}

	public LicenseEvaluation EvaluateLocal()
	{
		var outEval = new LicenseEvaluation
		{
			Allowed = false,
			Status = LicenseStatus.NotActivated
		};

		LicenseLeaseState state = _store.Get();
		string hw = GetHardwareKey();

		if (!string.IsNullOrWhiteSpace(state.LeaseJwt))
		{
			try
			{
				JwtSecurityToken jwt = _verifier.Verify(state.LeaseJwt!, hw);
				outEval.Allowed = true;
				outEval.Status = LicenseStatus.Active;
				outEval.CustomerName = GetClaim(jwt, "customer") ?? state.CustomerName;
				outEval.Message = state.Message ?? "Licensed";
				outEval.LeaseExpiresAt = jwt.ValidTo.ToUniversalTime().ToString("o");

				string? licenseExpClaim = GetClaim(jwt, "licenseExp");
				if (long.TryParse(licenseExpClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out long epoch))
				{
					DateTime expUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
					outEval.LicenseExpiresAt = expUtc.ToString("o");
					double days = (expUtc - DateTime.UtcNow).TotalDays;
					if (days <= 30 && days >= 0)
					{
						outEval.Status = LicenseStatus.Expiring;
					}
					else if (days < 0)
					{
						outEval.Status = LicenseStatus.Grace;
					}
				}
				return outEval;
			}
			catch (Exception leaseEx)
			{
				bool sameMachine = !string.IsNullOrEmpty(state.HardwareKey) &&
				                   string.Equals(state.HardwareKey, hw, StringComparison.OrdinalIgnoreCase);
				if (sameMachine && state.LastSuccessAt.HasValue)
				{
					DateTimeOffset graceDeadline = state.LastSuccessAt.Value.AddHours(LicenseConstants.OfflineGraceHours);
					if (DateTimeOffset.UtcNow < graceDeadline)
					{
						outEval.Allowed = true;
						outEval.Status = LicenseStatus.Grace;
						outEval.Message = "Offline grace period — reconnect to admin.evolabs.co";
						outEval.GraceUntil = graceDeadline.ToString("o");
						outEval.CustomerName = state.CustomerName;
						outEval.LicenseExpiresAt = state.LicenseExpiresAt;
						return outEval;
					}
				}
				outEval.Status = LicenseStatus.Invalid;
				outEval.Message = leaseEx.Message;
				return outEval;
			}
		}

		if (string.Equals(state.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
		{
			outEval.Status = LicenseStatus.Pending;
			outEval.Message = state.Message ?? "Waiting for Evolabs approval";
			return outEval;
		}

		outEval.Message = state.Message ?? "Not activated";
		return outEval;
	}

	public async Task ScheduledRefreshAsync()
	{
		if (!LicenseConstants.EnforcementEnabled)
		{
			return;
		}
		LicenseLeaseState state = _store.Get();
		if (string.IsNullOrWhiteSpace(state.LeaseJwt) &&
		    !string.Equals(state.Status, "PENDING", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals(state.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals(state.Status, "CACHED", StringComparison.OrdinalIgnoreCase) &&
		    !string.Equals(state.Status, "GRACE", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		try
		{
			await CheckWithPortalAsync().ConfigureAwait(false);
		}
		catch
		{
			// ignore scheduled failures
		}
	}

	private static string? Str(Dictionary<string, object?> map, string key)
	{
		return map.TryGetValue(key, out object? v) ? v?.ToString() : null;
	}

	private static string? GetClaim(JwtSecurityToken jwt, string type)
	{
		foreach (var claim in jwt.Claims)
		{
			if (string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))
			{
				return claim.Value;
			}
		}
		return null;
	}
}

public sealed class LicenseEvaluation
{
	public bool Allowed { get; set; }
	public LicenseStatus Status { get; set; }
	public string? Message { get; set; }
	public string? CustomerName { get; set; }
	public string? LicenseExpiresAt { get; set; }
	public string? LeaseExpiresAt { get; set; }
	public string? GraceUntil { get; set; }
	public string? HardwareKey { get; set; }
	public string? ProductCode { get; set; }
	public string? Portal { get; set; }
	public string? AppVersion { get; set; }
}
