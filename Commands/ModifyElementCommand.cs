using System.Windows;
using System.Windows.Controls;

namespace EvoDesigner.Commands
{
    public class ModifyElementCommand : ICommand
    {
        private readonly UIElement _element;
        private readonly object _oldValue;
        private readonly object _newValue;
        private readonly string _propertyName;
        private readonly System.Action<object> _setter;

        public string Description { get; }

        public ModifyElementCommand(UIElement element, string propertyName, object oldValue, object newValue, System.Action<object> setter, string description = "Modify element")
        {
            _element = element;
            _propertyName = propertyName;
            _oldValue = oldValue;
            _newValue = newValue;
            _setter = setter;
            Description = description;
        }

        public void Execute()
        {
            _setter(_newValue);
        }

        public void Undo()
        {
            _setter(_oldValue);
        }
    }
}
