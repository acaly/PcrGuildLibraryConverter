using GuildLibraryConverter.Data;
using GuildLibraryConverter.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.UI.ViewModels
{
    class SyncTab : AbstactTabItem
    {
        public class SyncItem : ReactiveObject
        {
            private readonly SyncTab _parent;
            private readonly Data.SyncItem _syncItem;

            public string DisplayName => _syncItem.DisplayName;
            public string DocUrl => _syncItem.QQDocumentUrl;
            public string GitFileName => _syncItem.GitDataFileName;
            public string GitRawFolderName => _syncItem.GitRawDataFolderName;
            public ActionCommand SyncCommand { get; }

            private bool _isBusy;
            public bool IsBusy
            {
                get => _isBusy;
                private set => SetProperty(nameof(IsBusy), ref _isBusy, value);
            }

            private DateTime _lastSave = DateTime.Now;
            public DateTime LastSave
            {
                get => _lastSave;
                private set => SetProperty(nameof(LastSave), ref _lastSave, value);
            }

            public SyncItem(SyncTab parent, Data.SyncItem syncItem)
            {
                _parent = parent;
                _syncItem = syncItem;

                SyncCommand = new(Sync);
            }

            private async void Sync()
            {
                var mainViewModel = _parent._parent;
                IsBusy = true;
                SyncCommand.CanExecute = false;

                var newTab = await Task.Run(DoSync);
                mainViewModel.TabItems.Add(newTab);

                IsBusy = false;
                SyncCommand.CanExecute = true;
            }

            private async Task<AbstactTabItem> DoSync()
            {
                string loadingStageDescription = "";
                try
                {
                    //1. Download from doc.qq
                    loadingStageDescription = "下载腾讯文档数据";
                    var (newData, rawData) = await QQDocHelper.DownloadFromQQDocument(DocUrl);

                    //2. Get current version from git repo (create if not exsits, sync otherwise).
                    loadingStageDescription = "下载镜像数据";
                    var oldData = new List<Team>();
                    var gitAbsPath = Path.Combine(Environment.CurrentDirectory, _parent._config.GitLocalRepoPath);
                    var gitFile = Path.Combine(gitAbsPath, GitFileName);
                    if (!File.Exists(gitFile))
                    {
                        if (Directory.Exists(gitAbsPath))
                        {
                            throw new IOException($"Git仓库{gitAbsPath}已经创建，但不包括所选数据文件{GitFileName}。");
                        }
                        GitRepoHelper.Clone(_parent._config.GitRemoteRepoPath, gitAbsPath, _parent._config.GitUser);
                        if (!File.Exists(gitFile))
                        {
                            throw new IOException($"Git仓库{gitAbsPath}已经创建，但不包括所选数据文件{GitFileName}。");
                        }
                    }
                    else
                    {
                        GitRepoHelper.Pull(gitAbsPath, _parent._config.GitUser);
                    }
                    await oldData.DeserializeAsync(gitFile);

                    //3. Compare and return the diff list.
                    loadingStageDescription = "计算新版本内容差异";
                    var diff = TeamHelper.CalculateDiff(oldData, newData);
                    var ret = new DiffList(_parent._parent, $"修改 {DisplayName} - {DateTime.Now:HHmmss}", diff);

                    //4. Write data to local git repo.
                    loadingStageDescription = "写入本地文件";
                    File.Delete(gitFile);
                    await newData.SerializeAsync(gitFile);
                    {
                        var rawDataDir = Path.Combine(gitAbsPath, _syncItem.GitRawDataFolderName);
                        if (Directory.Exists(rawDataDir))
                        {
                            Directory.Delete(rawDataDir, recursive: true);
                        }
                        Directory.CreateDirectory(rawDataDir);
                        var indexFilename = Path.Combine(rawDataDir, "index.json");
                        await TeamHelper.SerializeRawDataIndex(rawData, indexFilename);
                        for (int i = 0; i < rawData.Count; ++i)
                        {
                            var (url, filename, bytes) = rawData[i];
                            await File.WriteAllBytesAsync(Path.Combine(rawDataDir, filename), bytes);
                        }
                    }

                    //5. If all above succeeded, upload.
                    loadingStageDescription = "上传镜像数据";
                    GitRepoHelper.Commit(gitAbsPath, _parent._config.GitUser);
                    GitRepoHelper.Push(gitAbsPath, _parent._config.GitUser);

                    return ret;
                }
                catch (QQDocDownloadException docException)
                {
                    return new ErrorList(_parent._parent, $"错误 {DisplayName} - {DateTime.Now:HHmmss}")
                    {
                        Errors = docException.Errors,
                    };
                }
                catch (Exception e)
                {
                    return new ErrorList(_parent._parent, $"错误 {DisplayName} - {DateTime.Now:HHmmss}")
                    {
                        Errors = new[]
                        {
                            new QQDocDownloadError
                            {
                                ErrorType = QQDocDownloadErrorType.ClrException,
                                Title = $"{loadingStageDescription}时发生错误：{e.Message}",
                                Details = e.StackTrace,
                            },
                        },
                    };
                }
            }
        }

        public static readonly SyncTab DesignerInstance = DesignerData.SyncTab;

        private readonly MainViewModel _parent;
        private readonly AppConfig _config;
        public List<SyncItem> SyncItems { get; } = new();

        public SyncTab(MainViewModel parent, AppConfig config) : base("同步", canClose: false)
        {
            _parent = parent;
            _config = config;
            foreach (var item in config.SyncItems)
            {
                SyncItems.Add(new(this, item));
            }
        }
    }
}
