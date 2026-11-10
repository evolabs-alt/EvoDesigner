using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TaggJack.Commands;

public class DeleteElementsCommand : ICommand
{
	private struct ElementState
	{
		public UIElement Element;

		public double Left;

		public double Top;
	}

	private readonly Canvas _canvas;

	private readonly List<ElementState> _elementStates;

	public string Description { get; }

	public DeleteElementsCommand(Canvas canvas, IEnumerable<UIElement> elements, string description = "Delete elements")
	{
		_canvas = canvas;
		_elementStates = elements.Select((UIElement e) => new ElementState
		{
			Element = e,
			Left = Canvas.GetLeft(e),
			Top = Canvas.GetTop(e)
		}).ToList();
		Description = description;
	}

	public void Execute()
	{
		foreach (ElementState elementState in _elementStates)
		{
			_canvas.Children.Remove(elementState.Element);
		}
	}

	public void Undo()
	{
		foreach (ElementState elementState in _elementStates)
		{
			Canvas.SetLeft(elementState.Element, elementState.Left);
			Canvas.SetTop(elementState.Element, elementState.Top);
			_canvas.Children.Add(elementState.Element);
		}
	}
}
