namespace TaggJack.Models;

public static class TemplateTypeExtensions
{
	public static bool IsInkjetLike(this TemplateType type)
	{
		if ((uint)(type - 1) <= 1u)
		{
			return true;
		}
		return false;
	}

	public static bool IsMarkemImaje(this TemplateType type)
	{
		return type == TemplateType.Inkjet;
	}

	public static bool IsLinx(this TemplateType type)
	{
		return type == TemplateType.Linx;
	}

	public static string DisplayName(this TemplateType type)
	{
		return type switch
		{
			TemplateType.Inkjet => "Markem-Imaje", 
			TemplateType.Linx => "Linx", 
			_ => "Standard", 
		};
	}

	public static int DefaultPrinterPort(this TemplateType type)
	{
		return type switch
		{
			TemplateType.Linx => 29043, 
			TemplateType.Inkjet => 2000, 
			_ => 2000, 
		};
	}
}
