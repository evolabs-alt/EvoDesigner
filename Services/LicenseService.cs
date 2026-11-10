using System.Collections.Generic;
using System.Threading.Tasks;
using TaggJack.Models;
using TaggJack.Services.License;

namespace TaggJack.Services;

/// <summary>
/// Facade over portal licensing. Offline TJ11/AES activation has been removed.
/// </summary>
public class LicenseService
{
	private readonly PortalLicenseService _portal = PortalLicenseService.Instance;

	public LicenseEvaluation GetStatus() => _portal.GetStatus();

	public (LicenseStatus Status, LicenseEvaluation Eval) GetLicenseStatus()
	{
		LicenseEvaluation eval = _portal.GetStatus();
		return (eval.Status, eval);
	}

	public bool IsOperationAllowed() => _portal.IsOperationAllowed();

	public void RequireLicensed() => _portal.RequireLicensed();

	public Task<Dictionary<string, object?>> RequestActivationAsync(string? customerHint, string? contact) =>
		_portal.RequestActivationAsync(customerHint, contact);

	public Task<Dictionary<string, object?>> CheckWithPortalAsync() =>
		_portal.CheckWithPortalAsync();

	public Task RefreshAsync() => _portal.ScheduledRefreshAsync();

	public string GetHardwareKey() => _portal.GetHardwareKey();
}
