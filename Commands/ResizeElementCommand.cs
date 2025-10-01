using System.Windows;
using System.Windows.Controls;

namespace EvoDesigner.Commands
{
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
            
            // Update line element if this is a line container
            if (_element is Canvas lineCanvas && lineCanvas.Children.Count > 0 && lineCanvas.Children[0] is System.Windows.Shapes.Line lineElement)
            {
                lineElement.X2 = _newWidth;
                lineElement.Y1 = _newHeight / 2;
                lineElement.Y2 = _newHeight / 2;
            }
        }

        public void Undo()
        {
            _element.Width = _oldWidth;
            _element.Height = _oldHeight;
            
            // Update line element if this is a line container
            if (_element is Canvas lineCanvas && lineCanvas.Children.Count > 0 && lineCanvas.Children[0] is System.Windows.Shapes.Line lineElement)
            {
                lineElement.X2 = _oldWidth;
                lineElement.Y1 = _oldHeight / 2;
                lineElement.Y2 = _oldHeight / 2;
            }
        }
    }
}