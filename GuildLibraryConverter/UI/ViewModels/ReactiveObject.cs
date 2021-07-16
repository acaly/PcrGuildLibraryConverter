using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.UI.ViewModels
{
    class ReactiveObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new(propertyName));
        }

        protected void SetProperty<T>(string propertyName, ref T field, T value) where T : IEquatable<T>
        {
            if (!field.Equals(value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }
    }
}
