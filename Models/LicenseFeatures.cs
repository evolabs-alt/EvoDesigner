using System.Collections.Generic;

namespace TaggJack.Models;

public static class LicenseFeatures
{
	public const string BASIC_EDITING = "BasicEditing";

	public const string ADVANCED_EDITING = "AdvancedEditing";

	public const string DATABASE_INTEGRATION = "DatabaseIntegration";

	public const string SCHEDULED_PRINTING = "ScheduledPrinting";

	public const string BATCH_PRINTING = "BatchPrinting";

	public const string TEMPLATE_IMPORT_EXPORT = "TemplateImportExport";

	public const string BARCODE_GENERATION = "BarcodeGeneration";

	public const string QR_CODE_GENERATION = "QRCodeGeneration";

	public static List<string> GetFeaturesForLicenseType(LicenseType licenseType)
	{
		return new List<string> { "BasicEditing", "AdvancedEditing", "DatabaseIntegration", "ScheduledPrinting", "BatchPrinting", "TemplateImportExport", "BarcodeGeneration", "QRCodeGeneration" };
	}
}
