using System;

namespace TaggJack.Commands;

public class PropertyChangeCommand : ICommand
{
	private readonly object _target;

	private readonly string _propertyName;

	private readonly object _oldValue;

	private readonly object _newValue;

	private readonly Action<object> _applyValue;

	public string Description { get; }

	public PropertyChangeCommand(object target, string propertyName, object oldValue, object newValue, Action<object> applyValue, string? description = null)
	{
		_target = target;
		_propertyName = propertyName;
		_oldValue = oldValue;
		_newValue = newValue;
		_applyValue = applyValue;
		Description = description ?? ("Change " + propertyName);
	}

	public void Execute()
	{
		_applyValue(_newValue);
	}

	public void Undo()
	{
		_applyValue(_oldValue);
	}
}
