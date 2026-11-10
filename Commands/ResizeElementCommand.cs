using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace TaggJack.Commands;

public class ResizeElementCommand : ICommand
{
	private readonly FrameworkElement _element;

	private readonly double _oldWidth;

	private readonly double _oldHeight;

	private readonly double _newWidth;

	private readonly double _newHeight;

	public string Description { get; }

	public ResizeElementCommand(FrameworkElement element, double newWidth, double newHeight, string description = "Resize element")
	{
		_element = element;
		_oldWidth = element.Width;
		_oldHeight = element.Height;
		_newWidth = newWidth;
		_newHeight = newHeight;
		Description = description;
	}

	public void Execute()
	{
		_element.Width = _newWidth;
		_element.Height = _newHeight;
		if (_element is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Line line)
		{
			line.X2 = _newWidth;
			line.Y1 = _newHeight / 2.0;
			line.Y2 = _newHeight / 2.0;
		}
	}

	public void Undo()
	{
		_element.Width = _oldWidth;
		_element.Height = _oldHeight;
		if (_element is Canvas canvas && canvas.Children.Count > 0 && canvas.Children[0] is Line line)
		{
			line.X2 = _oldWidth;
			line.Y1 = _oldHeight / 2.0;
			line.Y2 = _oldHeight / 2.0;
		}
	}
}
