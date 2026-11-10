using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace TaggJack.Services.License;

public sealed class LicenseIntegrityService
{
	private static readonly string[] CriticalTypeNames =
	{
		"TaggJack.Services.License.PortalLicenseService",
		"TaggJack.Services.License.LicenseLeaseVerifier",
		"TaggJack.Services.License.LicenseConstants"
	};

	private string? _cached;

	public string GetIntegrityDigest()
	{
		if (_cached != null)
		{
			return _cached;
		}
		try
		{
			using var sha = SHA256.Create();
			Assembly asm = typeof(LicenseIntegrityService).Assembly;
			foreach (string typeName in CriticalTypeNames)
			{
				Type? t = asm.GetType(typeName);
				string token = t?.FullName ?? typeName;
				byte[] bytes = Encoding.UTF8.GetBytes(token + "|" + asm.ManifestModule.ModuleVersionId);
				sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
			}
			try
			{
				string? loc = asm.Location;
				if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
				{
					byte[] file = File.ReadAllBytes(loc);
					sha.TransformBlock(file, 0, Math.Min(file.Length, 64 * 1024), null, 0);
				}
			}
			catch
			{
			}
			sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			_cached = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
			return _cached;
		}
		catch
		{
			return "unavailable";
		}
	}
}
