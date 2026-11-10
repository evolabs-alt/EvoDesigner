using System.Collections.Generic;

namespace TaggJack.Models;

public class InkjetConfig
{
	public string PrinterHost { get; set; } = "127.0.0.1";

	public int PrinterPort { get; set; } = 2000;

	public string JobName { get; set; } = "TEST";

	public int JobId { get; set; } = 1;

	public int VarWidth { get; set; } = 10;

	public List<InkjetPositionMapping> Mappings { get; set; } = new List<InkjetPositionMapping>();
}
