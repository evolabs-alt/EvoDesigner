using System;
using TaggJack.Models;

namespace TaggJack.Services;

public class TemplateManagerEventArgs : EventArgs
{
	public LoadedTemplate Template { get; }

	public TemplateManagerEventArgs(LoadedTemplate template)
	{
		Template = template;
	}
}
