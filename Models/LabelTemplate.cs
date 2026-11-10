using System.Collections.Generic;

namespace TaggJack.Models;

public class LabelTemplate
{
	public string TemplateName { get; set; } = "";

	public TemplateType TemplateType { get; set; }

	public double LabelWidth { get; set; }

	public double LabelHeight { get; set; }

	public double MarginLeft { get; set; }

	public double MarginRight { get; set; }

	public double MarginTop { get; set; }

	public double MarginBottom { get; set; }

	public List<LabelElement> Elements { get; set; } = new List<LabelElement>();

	public List<FieldMapping> FieldMappings { get; set; } = new List<FieldMapping>();

	public DataSourceConfig? DataSource { get; set; }

	public InkjetConfig? InkjetConfig { get; set; }
}
