using System.Collections.Generic;

namespace EvoDesigner.Commands
{
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
            foreach (var command in _commands)
            {
                command.Execute();
            }
        }

        public void Undo()
        {
            // Undo in reverse order
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }
    }
}
