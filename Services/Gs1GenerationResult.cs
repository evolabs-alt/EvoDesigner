using System.Collections.Generic;
using TaggJack.Models;

namespace TaggJack.Services;

public class Gs1GenerationResult
{
	public int Level1Count { get; set; }

	public int Level2Count { get; set; }

	public int Level3Count { get; set; }

	public List<Gs1SerialRecord> Level1Serials { get; set; } = new List<Gs1SerialRecord>();

	public List<Gs1SerialRecord> Level2Serials { get; set; } = new List<Gs1SerialRecord>();

	public List<Gs1SerialRecord> Level3Serials { get; set; } = new List<Gs1SerialRecord>();

	public string RawResponseJson { get; set; } = "";

	public List<Gs1SerialRecord> AllSerials()
	{
		List<Gs1SerialRecord> list = new List<Gs1SerialRecord>();
		list.AddRange(Level1Serials);
		list.AddRange(Level2Serials);
		list.AddRange(Level3Serials);
		return list;
	}
}
