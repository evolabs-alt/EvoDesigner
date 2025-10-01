using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EvoDesigner.Commands
{
    public class MoveElementsCommand : ICommand
    {
        private readonly List<ElementPosition> _elementPositions;

        public string Description { get; }

        private struct ElementPosition
        {
            public UIElement Element;
            public double OriginalLeft;
            public double OriginalTop;
            public double NewLeft;
            public double NewTop;
        }

        public MoveElementsCommand(IEnumerable<UIElement> elements, double deltaX, double deltaY, string description = "Move elements")
        {
            _elementPositions = elements.Select(e => 
            {
                var originalLeft = Canvas.GetLeft(e);
                var originalTop = Canvas.GetTop(e);
                var newLeft = originalLeft + deltaX;
                var newTop = originalTop + deltaY;
                
                // Apply boundary constraints if element has a parent canvas
                if (e is FrameworkElement fe && fe.Parent is Canvas canvas)
                {
                    newLeft = System.Math.Max(0, System.Math.Min(newLeft, canvas.ActualWidth - fe.ActualWidth));
                    newTop = System.Math.Max(0, System.Math.Min(newTop, canvas.ActualHeight - fe.ActualHeight));
                }
                
                return new ElementPosition
                {
                    Element = e,
                    OriginalLeft = originalLeft,
                    OriginalTop = originalTop,
                    NewLeft = newLeft,
                    NewTop = newTop
                };
            }).ToList();
            Description = description;
        }

        // Constructor that accepts explicit original and current positions (for drag operations)
        public MoveElementsCommand(IEnumerable<(UIElement element, double originalLeft, double originalTop)> elementPositions, string description = "Move elements")
        {
            _elementPositions = elementPositions.Select(pos => 
            {
                var currentLeft = Canvas.GetLeft(pos.element);
                var currentTop = Canvas.GetTop(pos.element);
                
                return new ElementPosition
                {
                    Element = pos.element,
                    OriginalLeft = pos.originalLeft,
                    OriginalTop = pos.originalTop,
                    NewLeft = currentLeft,
                    NewTop = currentTop
                };
            }).ToList();
            Description = description;
        }

        public void Execute()
        {
            foreach (var pos in _elementPositions)
            {
                Canvas.SetLeft(pos.Element, pos.NewLeft);
                Canvas.SetTop(pos.Element, pos.NewTop);
            }
        }

        public void Undo()
        {
            foreach (var pos in _elementPositions)
            {
                Canvas.SetLeft(pos.Element, pos.OriginalLeft);
                Canvas.SetTop(pos.Element, pos.OriginalTop);
            }
        }
    }
}
