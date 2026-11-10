namespace TaggJack.Models;

public enum LicenseStatus
{
	NotActivated,
	Pending,
	Active,
	Expiring,
	Grace,
	Invalid,
	Unreachable,

	// Legacy aliases kept so older call sites compile if any remain
	Valid = Active,
	Expired = Invalid,
	HardwareMismatch = Invalid
}
