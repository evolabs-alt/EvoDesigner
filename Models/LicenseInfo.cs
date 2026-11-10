using System;
using System.Collections.Generic;

namespace TaggJack.Models;

public class LicenseInfo
{
	public string LicenseKey { get; set; } = "";

	public LicenseType LicenseType { get; set; }

	public string MachineId { get; set; } = "";

	public DateTime ActivationDate { get; set; }

	public DateTime? ExpiryDate { get; set; }

	public string CustomerEmail { get; set; } = "";

	public string CustomerName { get; set; } = "";

	public int MaxActivations { get; set; }

	public int ActivationCount { get; set; }

	public List<string> EnabledFeatures { get; set; } = new List<string>();

	public string Signature { get; set; } = "";

	public bool IsExpired
	{
		get
		{
			if (!ExpiryDate.HasValue)
			{
				return false;
			}
			return DateTime.Now > ExpiryDate.Value;
		}
	}

	public int DaysRemaining
	{
		get
		{
			if (!ExpiryDate.HasValue)
			{
				return int.MaxValue;
			}
			int days = (ExpiryDate.Value - DateTime.Now).Days;
			if (days <= 0)
			{
				return 0;
			}
			return days;
		}
	}

	public bool IsFeatureEnabled(string feature)
	{
		return EnabledFeatures.Contains(feature);
	}
}
