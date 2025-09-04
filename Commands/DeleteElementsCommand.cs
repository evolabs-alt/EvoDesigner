using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EvoDesigner.Commands
{
    public class DeleteElementsCommand : ICommand
    {
        private readonly Canvas _canvas;
        private readonly List<ElementState> _elementStates;

        public string Description { get; }

        private struct ElementState
        {
            public UIElement Element;
            public double Left;
            public double Top;
        }

        public DeleteElementsCommand(Canvas canvas, IEnumerable<UIElement> elements, string description = "Delete elements")
        {
            _canvas = canvas;
            _elementStates = elements.Select(e => new ElementState
            {
                Element = e,
                Left = Canvas.GetLeft(e),
                Top = Canvas.GetTop(e)
            }).ToList();
            Description = description;
        }

        public void Execute()
        {
            foreach (var state in _elementStates)
            {
                _canvas.Children.Remove(state.Element);
            }
        }

        public void Undo()
        {
            foreach (var state in _elementStates)
            {
                Canvas.SetLeft(state.Element, state.Left);
                Canvas.SetTop(state.Element, state.Top);
                _canvas.Children.Add(state.Element);
            }
        }
    }
}
