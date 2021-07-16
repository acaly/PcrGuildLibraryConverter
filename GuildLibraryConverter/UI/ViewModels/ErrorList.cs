using GuildLibraryConverter.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.UI.ViewModels
{
    class ErrorList : AbstactTabItem
    {
        public static readonly ErrorList DesignerInstance = DesignerData.ErrorList;

        private readonly MainViewModel _parent;
        public QQDocDownloadError[] Errors { get; init; }

        public ErrorList(MainViewModel parent, string title) : base(title, canClose: true)
        {
            _parent = parent;
        }

        public override void Close()
        {
            _parent.TabItems.Remove(this);
        }
    }
}
