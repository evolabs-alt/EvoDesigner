using System.Collections.Generic;

namespace EvoDesigner.Commands
{
    public class CommandHistory
    {
        private readonly List<ICommand> _commands = new List<ICommand>();
        private int _currentIndex = -1;
        private const int MaxHistorySize = 50;

        public bool CanUndo => _currentIndex >= 0;
        public bool CanRedo => _currentIndex < _commands.Count - 1;

        public void ExecuteCommand(ICommand command)
        {
            // Remove any commands after the current index (for branching undo/redo)
            if (_currentIndex < _commands.Count - 1)
            {
                _commands.RemoveRange(_currentIndex + 1, _commands.Count - _currentIndex - 1);
            }

            // Add the new command
            _commands.Add(command);
            _currentIndex++;

            // Execute the command
            command.Execute();

            // Limit history size
            if (_commands.Count > MaxHistorySize)
            {
                _commands.RemoveAt(0);
                _currentIndex--;
            }
        }

        public void Undo()
        {
            if (CanUndo)
            {
                _commands[_currentIndex].Undo();
                _currentIndex--;
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                _currentIndex++;
                _commands[_currentIndex].Execute();
            }
        }

        public void Clear()
        {
            _commands.Clear();
            _currentIndex = -1;
        }

        public string GetUndoDescription()
        {
            return CanUndo ? _commands[_currentIndex].Description : "Nothing to undo";
        }

        public string GetRedoDescription()
        {
            return CanRedo ? _commands[_currentIndex + 1].Description : "Nothing to redo";
        }
    }
}
