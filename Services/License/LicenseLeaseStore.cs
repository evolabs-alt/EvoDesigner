using System;
using System.IO;
using System.Text;

namespace TaggJack.Services.License;

public sealed class LicenseLeaseStore
{
	private readonly object _gate = new();
	private LicenseLeaseState _state = new();

	public LicenseLeaseStore()
	{
		LoadFromDisk();
	}

	public LicenseLeaseState Get()
	{
		lock (_gate)
		{
			return _state.Clone();
		}
	}

	public void Update(LicenseLeaseState next)
	{
		lock (_gate)
		{
			_state = next.Clone();
			if (!string.IsNullOrWhiteSpace(next.LeaseJwt))
			{
				Persist(next.LeaseJwt!, next.LastSuccessAt, next.HardwareKey);
			}
		}
	}

	public void SetPending(string message)
	{
		lock (_gate)
		{
			_state = _state.Clone();
			_state.Status = "PENDING";
			_state.Message = message;
		}
	}

	public void SetInvalid(string message)
	{
		lock (_gate)
		{
			_state = _state.Clone();
			_state.Status = "INVALID";
			_state.Message = message;
			_state.LeaseJwt = null;
			TryDeleteCache();
		}
	}

	private void Persist(string jwt, DateTimeOffset? lastSuccess, string? hardwareKey)
	{
		try
		{
			EnsureDir();
			long epoch = (lastSuccess ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
			string content = jwt + "\n" + epoch + "\n" + (hardwareKey ?? "");
			File.WriteAllText(CachePath(), content, Encoding.UTF8);
		}
		catch
		{
			// best-effort cache
		}
	}

	private void LoadFromDisk()
	{
		try
		{
			string path = CachePath();
			if (!File.Exists(path))
			{
				return;
			}
			string[] parts = File.ReadAllText(path, Encoding.UTF8).Trim().Split('\n');
			if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
			{
				return;
			}
			var s = new LicenseLeaseState
			{
				LeaseJwt = parts[0].Trim(),
				Status = "CACHED"
			};
			if (parts.Length >= 2 && long.TryParse(parts[1].Trim(), out long ms))
			{
				s.LastSuccessAt = DateTimeOffset.FromUnixTimeMilliseconds(ms);
			}
			if (parts.Length >= 3)
			{
				s.HardwareKey = parts[2].Trim();
			}
			_state = s;
		}
		catch
		{
			// ignore corrupt cache
		}
	}

	private void TryDeleteCache()
	{
		try
		{
			File.Delete(CachePath());
		}
		catch
		{
		}
	}

	private static void EnsureDir()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(CachePath())!);
	}

	private static string CachePath()
	{
		string dir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"TaggJack");
		return Path.Combine(dir, LicenseConstants.LeaseCacheFileName);
	}
}
