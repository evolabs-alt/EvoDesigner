namespace TaggJack.Services.License;

/// <summary>Hardcoded production license settings. Not overridable via config.json.</summary>
public static class LicenseConstants
{
	public const string PortalBaseUrl = "https://admin.evolabs.co";
	public const string ProductCode = "TAGGJACK";
	public const bool EnforcementEnabled = true;
	public const int RefreshHours = 12;
	public const int OfflineGraceHours = 72;
	public const string AppVersion = "1.1.0";
	public const string LeaseCacheFileName = "taggjack-license-lease.jwt";
	public const string PublicKeyResourceName = "TaggJack.Resources.keys.license-public.pem";
	public const string JwtIssuer = "admin.evolabs.co";
	public const string AllowlistedHost = "admin.evolabs.co";
}
