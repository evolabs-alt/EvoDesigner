using System.Collections.Generic;

namespace TaggJack.Commands;

public class BatchCommand : ICommand
{
	private readonly List<ICommand> _commands;

	public string Description { get; }

	public BatchCommand(List<ICommand> commands, string description = "Batch operation")
	{
		_commands = commands;
		Description = description;
	}

	public void Execute()
	{
		foreach (ICommand command in _commands)
		{
			command.Execute();
		}
	}

	public void Undo()
	{
		for (int num = _commands.Count - 1; num >= 0; num--)
		{
			_commands[num].Undo();
		}
	}
}
