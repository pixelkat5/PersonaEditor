using System;
using System.Windows.Input;

namespace AuxiliaryLibraries.WPF
{
    public class RelayCommand : ICommand
    {
        object action;

        public RelayCommand(Action<object> action)
        {
            this.action = action;
        }

        public RelayCommand(Action action)
        {
            this.action = action;
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        public void Execute(object parameter)
        {
            if (action is Action act)
                act();
            else if (action is Action<object> actobj)
                actobj(parameter);
        }
    }
}