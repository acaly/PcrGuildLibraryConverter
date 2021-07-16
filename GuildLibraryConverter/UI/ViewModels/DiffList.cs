using GuildLibraryConverter.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.UI.ViewModels
{
    class DiffListItem
    {
        public Team Item { get; init; }
        public TeamDiff ItemDiff { get; init; }
        public string Title => $"{Item.Boss} - {Item.Id}";

        public int DiffSign { get; init; }
        public bool IsAdded => DiffSign > 0;
        public bool IsModified => DiffSign == 0;
        public bool IsRemoved => DiffSign < 0;

        public bool HasCommentsDiff => ItemDiff?.CommentsDiff.Any ?? false;
        public List<string> AddedComments => ItemDiff?.CommentsDiff.Added ?? new();
        public List<string> RemovedComments => ItemDiff?.CommentsDiff.Removed ?? new();
        public List<(string Old, string New)> ModifiedComments => ItemDiff?.CommentsDiff.Modified ?? new();

        public bool HasSourcesDiff => ItemDiff?.SourcesDiff.Any ?? false;
        public List<Source> AddedSources => ItemDiff?.SourcesDiff.Added ?? new();
        public List<Source> RemovedSources => ItemDiff?.SourcesDiff.Removed ?? new();
        public List<(Source Old, Source New)> ModifiedSources => ItemDiff?.SourcesDiff.Modified ?? new();
    }

    class DiffList : AbstactTabItem
    {
        public static readonly DiffList DesignerInstance = DesignerData.DiffList;

        private readonly MainViewModel _parent;

        public List<DiffListItem> Items { get; } = new();

        public DiffList(MainViewModel parent, string title, ListDiff<Team, TeamDiff> diff) : base(title, canClose: true)
        {
            _parent = parent;
            foreach (var added in diff.Added)
            {
                Items.Add(new()
                {
                    Item = added,
                    DiffSign = 1,
                });
            }
            foreach (var modified in diff.Modified)
            {
                Items.Add(new()
                {
                    Item = modified.NewValue,
                    ItemDiff = modified,
                    DiffSign = 0,
                });
            }
            foreach (var removed in diff.Removed)
            {
                Items.Add(new()
                {
                    Item = removed,
                    DiffSign = -1,
                });
            }
        }

        public override void Close()
        {
            _parent.TabItems.Remove(this);
        }
    }
}
