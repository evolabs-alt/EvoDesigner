using System.Windows;
using System.Windows.Controls;

namespace TaggJack.Commands;

public class AddElementCommand : ICommand
{
	private readonly Canvas _canvas;

	private readonly UIElement _element;

	private readonly double _left;

	private readonly double _top;

	public string Description { get; }

	public AddElementCommand(Canvas canvas, UIElement element, double left, double top, string description = "Add element")
	{
		_canvas = canvas;
		_element = element;
		_left = left;
		_top = top;
		Description = description;
	}

	public void Execute()
	{
		Canvas.SetLeft(_element, _left);
		Canvas.SetTop(_element, _top);
		_canvas.Children.Add(_element);
	}

	public void Undo()
	{
		_canvas.Children.Remove(_element);
	}
}
