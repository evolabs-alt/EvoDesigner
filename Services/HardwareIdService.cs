using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace TaggJack.Services;

/// <summary>
/// Stable hardware fingerprint for portal licensing (SHA-256 hex uppercase),
/// matching CrewCycle: Windows MachineGuid + disk serial.
/// </summary>
public static class HardwareIdService
{
	private static string? _cachedKey;

	public static string GetHardwareKey()
	{
		if (_cachedKey != null)
		{
			return _cachedKey;
		}
		string fingerprint = CollectFingerprint();
		_cachedKey = Sha256Hex(fingerprint).ToUpperInvariant();
		return _cachedKey;
	}

	/// <summary>Alias for portal hardware key (full SHA-256 hex).</summary>
	public static string GetMachineId() => GetHardwareKey();

	public static string FormatMachineId(string machineId)
	{
		if (string.IsNullOrEmpty(machineId))
		{
			return machineId;
		}
		if (machineId.Length <= 16)
		{
			return machineId;
		}
		return machineId.Substring(0, 8) + "…" + machineId.Substring(machineId.Length - 8);
	}

	public static bool ValidateMachineId(string storedMachineId)
	{
		return string.Equals(storedMachineId, GetHardwareKey(), StringComparison.OrdinalIgnoreCase);
	}

	public static string GetHardwareInfo()
	{
		try
		{
			return $"MachineGuid: {ReadWindowsMachineGuid()}\nDisk: {ReadWindowsDiskSerial()}\nHardware Key: {GetHardwareKey()}";
		}
		catch (Exception ex)
		{
			return "Error retrieving hardware info: " + ex.Message;
		}
	}

	private static string CollectFingerprint()
	{
		var sb = new StringBuilder();
		sb.Append("os=windows|");
		sb.Append("machineGuid=").Append(ReadWindowsMachineGuid()).Append('|');
		sb.Append("disk=").Append(ReadWindowsDiskSerial()).Append('|');

		string compact = new string(sb.ToString().Where(char.IsLetterOrDigit).ToArray());
		if (compact.Length < 20)
		{
			sb.Append("userHome=").Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Append('|');
			sb.Append("osArch=").Append(Environment.Is64BitOperatingSystem ? "x64" : "x86").Append('|');
			sb.Append("cpu=").Append(ReadCpuId()).Append('|');
		}
		return sb.ToString();
	}

	private static string ReadWindowsMachineGuid()
	{
		try
		{
			using Process p = Process.Start(new ProcessStartInfo
			{
				FileName = "reg",
				Arguments = "query HKLM\\SOFTWARE\\Microsoft\\Cryptography /v MachineGuid",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			})!;
			string outText = p.StandardOutput.ReadToEnd();
			p.WaitForExit(5000);
			foreach (string line in outText.Split('\n'))
			{
				if (line.Contains("MachineGuid", StringComparison.OrdinalIgnoreCase))
				{
					string[] parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length > 0)
					{
						return parts[^1].Trim();
					}
				}
			}
		}
		catch
		{
		}
		return "";
	}

	private static string ReadWindowsDiskSerial()
	{
		try
		{
			using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
			foreach (ManagementObject obj in searcher.Get())
			{
				string? serial = obj["SerialNumber"]?.ToString()?.Trim();
				if (!string.IsNullOrWhiteSpace(serial))
				{
					return serial;
				}
			}
		}
		catch
		{
		}
		return "";
	}

	private static string ReadCpuId()
	{
		try
		{
			using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
			foreach (ManagementObject obj in searcher.Get())
			{
				return obj["ProcessorId"]?.ToString() ?? "";
			}
		}
		catch
		{
		}
		return "";
	}

	private static string Sha256Hex(string value)
	{
		byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}
