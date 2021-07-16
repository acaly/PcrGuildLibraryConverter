using GuildLibraryConverter.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GuildLibraryConverter.UI.ViewModels
{
    abstract class AbstactTabItem
    {
        public string Title { get; init; }
        public bool CanClose { get; init; }
        public virtual void Close() { }
        public ICommand CloseCommand => new ActionCommand(Close);

        public AbstactTabItem(string title, bool canClose)
        {
            Title = title;
            CanClose = canClose;
        }
    }
}
