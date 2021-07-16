using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.Data
{
    class SyncItem
    {
        public string DisplayName { get; set; }
        public string QQDocumentUrl { get; set; }
        public string GitDataFileName { get; set; }
        public string GitRawDataFolderName { get; set; }
    }

    class AppConfig
    {
        public string GitLocalRepoPath { get; set; }
        public string GitRemoteRepoPath { get; set; }
        public GitUserInfo GitUser { get; set; }

        public List<SyncItem> SyncItems { get; set; } = new();

        public static readonly AppConfig DesignerInstance = new()
        {
            GitLocalRepoPath = "git",
            GitRemoteRepoPath = "https://github.com/YOUR/REPO.git",
            GitUser = new()
            {
                UserName = "GIT_USERNAME",
                Email = "GIT_EMAIL",
                UploadLogin = "GIT_LOGIN",
                UploadPassword = "GIT_PASSWORD",
            },
            SyncItems =
            {
                new()
                {
                    DisplayName = "X阶段",
                    QQDocumentUrl = "https://docs.qq.com/sheet/XXXXXXXXXXXXXXXXX?tab=xxxxxx",
                    GitDataFileName = "data/202107/X.json",
                    GitRawDataFolderName = "raw/202107/X",
                },
            },
        };
    }
}
