using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace TaggJack.Services.License;

public sealed class LicenseLeaseVerifier
{
	private readonly RsaSecurityKey _key;

	public LicenseLeaseVerifier()
	{
		_key = new RsaSecurityKey(LoadPublicKey());
	}

	public JwtSecurityToken Verify(string leaseJwt, string expectedHardwareKey)
	{
		var handler = new JwtSecurityTokenHandler();
		var parameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = LicenseConstants.JwtIssuer,
			ValidateAudience = false,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = _key,
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(2)
		};

		handler.ValidateToken(leaseJwt, parameters, out SecurityToken validated);
		var jwt = (JwtSecurityToken)validated;

		string? hw = GetClaim(jwt, "hardwareKey") ?? jwt.Subject;
		if (string.IsNullOrWhiteSpace(expectedHardwareKey) ||
		    !string.Equals(expectedHardwareKey, hw, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Lease hardware key mismatch");
		}

		string? product = GetClaim(jwt, "product");
		if (!string.IsNullOrWhiteSpace(product) &&
		    !string.Equals(product, LicenseConstants.ProductCode, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Lease product mismatch");
		}

		return jwt;
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

	private static RSA LoadPublicKey()
	{
		Assembly asm = typeof(LicenseLeaseVerifier).Assembly;
		using Stream? stream = asm.GetManifestResourceStream(LicenseConstants.PublicKeyResourceName);
		if (stream == null)
		{
			throw new InvalidOperationException(
				$"Embedded public key resource not found: {LicenseConstants.PublicKeyResourceName}");
		}
		using var reader = new StreamReader(stream);
		string pem = reader.ReadToEnd();
		var rsa = RSA.Create();
		rsa.ImportFromPem(pem);
		return rsa;
	}
}
