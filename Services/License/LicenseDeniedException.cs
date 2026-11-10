using System;

namespace TaggJack.Services.License;

public sealed class LicenseDeniedException : Exception
{
	public LicenseDeniedException(string message) : base(message)
	{
	}
}
