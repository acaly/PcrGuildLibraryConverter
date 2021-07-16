using GuildLibraryConverter.Data;
using GuildLibraryConverter.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GuildLibraryConverter.UI.ViewModels
{
    class MainViewModel : ReactiveObject
    {
        public static MainViewModel DesignerInstance => DesignerData.MainViewModel;
        public static MainViewModel Instance { get; } = CreateInstance();

        public ObservableCollection<AbstactTabItem> TabItems { get; } = new();

        private bool _passwordDialogVisible = string.IsNullOrEmpty(App.Config.GitUser.UploadPassword);
        public bool PasswordDialogVisible
        {
            get => _passwordDialogVisible;
            private set => SetProperty(nameof(PasswordDialogVisible), ref _passwordDialogVisible, value);
        }

        public string Password
        {
            get => App.Config.GitUser.UploadPassword;
            set
            {
                App.Config.GitUser.UploadPassword = value;
                OnPropertyChanged(nameof(PasswordValueValid));
            }
        }

        public bool PasswordValueValid
        {
            get => Password?.Length > 0;
        }

        public ActionCommand ConfirmPasswordCommand
        {
            get => new(ConfirmPassword);
        }

        public static MainViewModel CreateInstance()
        {
            var ret = new MainViewModel();
            ret.TabItems.Add(new SyncTab(ret, App.Config));
            return ret;
        }

        private void ConfirmPassword()
        {
            if (Password?.Length > 0)
            {
                PasswordDialogVisible = false;
            }
        }
    }
}
