using GuildLibraryConverter.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.UI.ViewModels
{
    class DesignerData
    {
        public static readonly MainViewModel MainViewModel = CreateDesignerInstance();
        public static readonly SyncTab SyncTab = (SyncTab)MainViewModel.TabItems[0];
        public static readonly DiffList DiffList = (DiffList)MainViewModel.TabItems[1];
        public static readonly ErrorList ErrorList = (ErrorList)MainViewModel.TabItems[2];

        private static MainViewModel CreateDesignerInstance()
        {
            var ret = new MainViewModel();
            ret.TabItems.Add(new SyncTab(ret, AppConfig.DesignerInstance));
            ret.TabItems.Add(new DiffList(ret, "Test A - 174701", new()
            {
                Added =
                {
                    new Team
                    {
                        Boss = "C1",
                        Id = "C101",
                        Characters = new()
                        {
                            new Character { Id = 1 },
                            new Character { Id = 2 },
                            new Character { Id = 3 },
                            new Character { Id = 4 },
                            new Character { Id = 5 },
                        },
                        StandardDamage = 1000000,
                        Time = new() { Value = 90 },
                    },
                },
                Removed =
                {
                    new Team
                    {
                        Boss = "C2",
                        Id = "C201",
                        Characters = new()
                        {
                            new Character { Id = 1 },
                            new Character { Id = 2 },
                            new Character { Id = 3 },
                            new Character { Id = 4 },
                            new Character { Id = 5 },
                        },
                        StandardDamage = 2000000,
                        Time = new() { Value = 90 },
                    },
                },
                Modified =
                {
                    new TeamDiff
                    {
                        NewValue = new Team
                        {
                            Boss = "C1",
                            Id = "C102",
                            Characters = new()
                            {
                                new Character { Id = 1 },
                                new Character { Id = 2 },
                                new Character { Id = 3 },
                                new Character { Id = 4 },
                                new Character { Id = 5 },
                            },
                            StandardDamage = 1000000,
                            Time = new() { Value = 90 },
                        },
                        CommentsDiff = new()
                        {
                            Added = { "新备注" },
                            Modified = { ("修改备注", "旧备注") },
                            Removed = { "删除备注" },
                        },
                        StandardDamageDiff = 1,
                        TimeDiff = new() { Value = 89 },
                        SourcesDiff = new()
                        {
                            Added =
                            {
                                new()
                                {
                                    Author = "xxx",
                                    Description = "xxxx - xxxw",
                                    Damage = new() { Value = 1000000 },
                                },
                            },
                            Modified =
                            {
                                new()
                                {
                                    New = new()
                                    {
                                        Author = "yyy",
                                        Description = "yyyy - yyyw",
                                        Damage = new() { Value = 1000000 },
                                    },
                                    Old = new()
                                    {
                                        Author = "zzz",
                                        Description = "zzzz - zzzw",
                                        Damage = new() { Value = 2000000 },
                                    },
                                },
                            },
                            Removed =
                            {
                                new()
                                {
                                    Author = "www",
                                    Description = "wwww - wwww",
                                    Damage = new() { Value = 3000000 },
                                },
                            },
                        },
                    },
                },
            }));
            ret.TabItems.Add(new ErrorList(ret, "Error Test B - 175701")
            {
                Errors = new[]
                {
                    new QQDocDownloadError
                    {
                        ErrorType = QQDocDownloadErrorType.ClrException,
                        Title = "同步错误：XXXException",
                        Details = "    A.B()\n    A.B()",
                    },
                    new QQDocDownloadError
                    {
                        ErrorType = QQDocDownloadErrorType.CommentInvalidTarget,
                        Title = "原始数据格式错误：带箭头的备注指向无效的单元格",
                        CellCoordinate = "A1",
                        CellText = "text→",
                    },
                },
            });
            return ret;
        }
    }
}
