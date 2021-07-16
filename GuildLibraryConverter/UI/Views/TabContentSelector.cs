using GuildLibraryConverter.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GuildLibraryConverter.UI.Views
{
    class TabContentSelector : DataTemplateSelector
    {
        public static readonly TabContentSelector Instance = new();

        private readonly DataTemplate _syncTemplate;
        private readonly DataTemplate _diffTemplate;
        private readonly DataTemplate _errorTemplate;

        public TabContentSelector()
        {
            _syncTemplate = new()
            {
                VisualTree = new(typeof(SyncTabView)),
            };
            _diffTemplate = new()
            {
                VisualTree = new(typeof(DiffListView)),
            };
            _errorTemplate = new()
            {
                VisualTree = new(typeof(ErrorListView)),
            };
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is DiffList)
            {
                return _diffTemplate;
            }
            else if (item is SyncTab)
            {
                return _syncTemplate;
            }
            else if (item is ErrorList)
            {
                return _errorTemplate;
            }
            return null;
        }
    }
}
