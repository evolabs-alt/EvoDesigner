using TaggJack.Models;

namespace TaggJack.Services;

public static class FeatureFlags
{
	public static bool IsGs1Enabled(AppConfig config)
	{
		return config.Gs1Enabled;
	}

	public static bool IsErpImportEnabled(AppConfig config)
	{
		if (config.Gs1Enabled)
		{
			return config.ErpImportEnabled;
		}
		return false;
	}
}
