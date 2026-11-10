namespace TaggJack.Services;

public static class MeasurementService
{
	public const double SCREEN_DPI = 96.0;

	public const double PRINT_DPI = 204.0;

	public const double MM_TO_INCH = 0.03937007874015748;

	public const double PIXELS_PER_MM_SCREEN = 3.779527559055118;

	public const double PIXELS_PER_MM_PRINT = 8.031496062992126;

	public const double PRINT_SCALE_FACTOR = 2.125;

	public static double MmToScreenPixels(double mm)
	{
		return mm * 3.779527559055118;
	}

	public static double ScreenPixelsToMm(double pixels)
	{
		return pixels / 3.779527559055118;
	}

	public static double MmToPrintPixels(double mm)
	{
		return mm * 8.031496062992126;
	}

	public static double PrintPixelsToMm(double pixels)
	{
		return pixels / 8.031496062992126;
	}

	public static double ScreenToPrintPixels(double screenPixels)
	{
		return screenPixels * 2.125;
	}

	public static double PrintToScreenPixels(double printPixels)
	{
		return printPixels / 2.125;
	}

	public static string FormatMm(double mm)
	{
		return $"{mm:F1} mm";
	}
}
