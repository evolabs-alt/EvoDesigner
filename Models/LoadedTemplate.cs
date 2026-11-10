using System;

namespace TaggJack.Models;

public class LoadedTemplate
{
	public string Name { get; set; } = "";

	public string Path { get; set; } = "";

	public bool Enabled { get; set; } = true;

	public DateTime LastPrint { get; set; } = DateTime.MinValue;

	public DateTime DateAdded { get; set; } = DateTime.Now;
}
