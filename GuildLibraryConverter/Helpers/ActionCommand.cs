using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GuildLibraryConverter.Helpers
{
    class ActionCommand : ICommand
    {
        private readonly Action _action;

        public ActionCommand(Action action)
        {
            _action = action;
        }

        public ActionCommand(Func<Task> asyncAction)
        {
            _action = () => asyncAction();
        }

        bool ICommand.CanExecute(object parameter)
        {
            return _canExecute;
        }

        public void Execute(object parameter)
        {
            _action();
        }

        public event EventHandler CanExecuteChanged;

        private bool _canExecute = true;
        public bool CanExecute
        {
            get => _canExecute;
            set { _canExecute = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        }
    }
}
