using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LogCheck.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Func<Task>? _executeAsync;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute) : this(execute, null) { }

        public RelayCommand(Action execute, Func<bool>? canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync) : this(executeAsync, null) { }

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public async void Execute(object? parameter)
        {
            if (_execute != null)
            {
                _execute();
            }
            else if (_executeAsync != null)
            {
                await _executeAsync();
            }
        }
    }
}