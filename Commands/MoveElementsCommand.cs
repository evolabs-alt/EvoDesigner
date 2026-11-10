using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TaggJack.Commands;

public class MoveElementsCommand : ICommand
{
	private struct ElementPosition
	{
		public UIElement Element;

		public double OriginalLeft;

		public double OriginalTop;

		public double NewLeft;

		public double NewTop;
	}

	private readonly List<ElementPosition> _elementPositions;

	public string Description { get; }

	public MoveElementsCommand(IEnumerable<UIElement> elements, double deltaX, double deltaY, string description = "Move elements")
	{
		_elementPositions = elements.Select(delegate(UIElement e)
		{
			double left = Canvas.GetLeft(e);
			double top = Canvas.GetTop(e);
			double num = left + deltaX;
			double num2 = top + deltaY;
			if (e is FrameworkElement { Parent: Canvas parent } frameworkElement)
			{
				num = Math.Max(0.0, Math.Min(num, parent.ActualWidth - frameworkElement.ActualWidth));
				num2 = Math.Max(0.0, Math.Min(num2, parent.ActualHeight - frameworkElement.ActualHeight));
			}
			return new ElementPosition
			{
				Element = e,
				OriginalLeft = left,
				OriginalTop = top,
				NewLeft = num,
				NewTop = num2
			};
		}).ToList();
		Description = description;
	}

	public MoveElementsCommand(IEnumerable<(UIElement element, double originalLeft, double originalTop)> elementPositions, string description = "Move elements")
	{
		_elementPositions = elementPositions.Select<(UIElement, double, double), ElementPosition>(delegate((UIElement element, double originalLeft, double originalTop) pos)
		{
			double left = Canvas.GetLeft(pos.element);
			double top = Canvas.GetTop(pos.element);
			return new ElementPosition
			{
				Element = pos.element,
				OriginalLeft = pos.originalLeft,
				OriginalTop = pos.originalTop,
				NewLeft = left,
				NewTop = top
			};
		}).ToList();
		Description = description;
	}

	public void Execute()
	{
		foreach (ElementPosition elementPosition in _elementPositions)
		{
			Canvas.SetLeft(elementPosition.Element, elementPosition.NewLeft);
			Canvas.SetTop(elementPosition.Element, elementPosition.NewTop);
		}
	}

	public void Undo()
	{
		foreach (ElementPosition elementPosition in _elementPositions)
		{
			Canvas.SetLeft(elementPosition.Element, elementPosition.OriginalLeft);
			Canvas.SetTop(elementPosition.Element, elementPosition.OriginalTop);
		}
	}
}
