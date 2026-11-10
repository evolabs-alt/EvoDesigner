using System.Data;

namespace TaggJack.Services;

public class Gs1SampleDataResult
{
	public DataTable Table { get; set; } = new DataTable();

	public bool IsFromLastJob { get; set; }

	public long? JobId { get; set; }

	public int Level { get; set; }

	public string SourceDescription { get; set; } = "";
}
