using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

namespace GuildLibraryConverter.Data
{
    static class TeamHelper
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = new LowerCaseNamingPolicy(),
        };

        public class LowerCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                return Regex.Replace(name, "(.)([A-Z][a-z])", "$1_$2").ToLowerInvariant();
            }
        }

        private static ListDiff<T, (T Old, T New)> CalculateDiff<T, TSelect>(List<T> oldList, List<T> newList, Func<T, TSelect> selector)
            where T : IEquatable<T>
            where TSelect : IEquatable<TSelect>
        {
            ListDiff<T, (T Old, T New)> ret = new();
            Dictionary<TSelect, T> oldValues = new();
            foreach (var oldItem in oldList)
            {
                var oldItemSelected = selector(oldItem);
                if (!oldValues.TryAdd(oldItemSelected, oldItem))
                {
                    ret.Removed.Add(oldItem);
                }
            }

            foreach (var newItem in newList)
            {
                var newItemSelected = selector(newItem);
                if (oldValues.Remove(newItemSelected, out var oldItem))
                {
                    if (!oldItem.Equals(newItem))
                    {
                        ret.Modified.Add((oldItem, newItem));
                    }
                }
                else
                {
                    ret.Added.Add(newItem);
                }
            }
            ret.Removed.AddRange(oldValues.Values);

            return ret;
        }

        private static TeamDiff CalculateDiff(Team oldTeam, Team newTeam)
        {
            TeamDiff ret = new();
            ret.NewValue = newTeam;

            if (!newTeam.Time.Equals(oldTeam.Time))
            {
                ret.TimeDiff = oldTeam.Time;
            }

            ret.CommentsDiff = CalculateDiff(oldTeam.Comments, newTeam.Comments, str => str);
            ret.LabelsDiff = CalculateDiff(oldTeam.Labels, newTeam.Labels, str => str);
            if (newTeam.StandardDamage != oldTeam.StandardDamage)
            {
                ret.StandardDamageDiff = oldTeam.StandardDamage;
            }
            ret.SourcesDiff = CalculateDiff(oldTeam.Sources, newTeam.Sources, s => (s.Author, s.Description));

            return ret;
        }

        public static ListDiff<Team, TeamDiff> CalculateDiff(List<Team> oldList, List<Team> newList)
        {
            ListDiff<Team, TeamDiff> ret = new();
            Dictionary<string, Team> oldTeamDict = new();
            foreach (var oldTeam in oldList)
            {
                if (!oldTeamDict.TryAdd(oldTeam.Id, oldTeam))
                {
                    ret.Removed.Add(oldTeam);
                }
            }

            foreach (var newTeam in newList)
            {
                if (oldTeamDict.Remove(newTeam.Id, out var oldTeam))
                {
                    if (oldTeam.Boss == newTeam.Boss &&
                        oldTeam.Characters.SequenceEqual(newTeam.Characters, Character.IdComparer))
                    {
                        var diff = CalculateDiff(oldTeam, newTeam);
                        if (diff.Any)
                        {
                            ret.Modified.Add(diff);
                        }
                    }
                    else
                    {
                        ret.Removed.Add(oldTeam);
                        ret.Added.Add(newTeam);
                    }
                }
                else
                {
                    ret.Added.Add(newTeam);
                }
            }
            ret.Removed.AddRange(oldTeamDict.Values);

            return ret;
        }

        public static async Task SerializeAsync(this List<Team> data, string filename)
        {
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            using var file = File.OpenWrite(filename);
            file.SetLength(0);
            await JsonSerializer.SerializeAsync(file, data, _jsonOptions);
        }

        public static async Task DeserializeAsync(this List<Team> data, string filename)
        {
            using var file = File.OpenRead(filename);
            var read = await JsonSerializer.DeserializeAsync<List<Team>>(file, _jsonOptions);
            data.Clear();
            data.AddRange(read);
        }

        private record SerializedRawDataInfo(string Url, string Filename);

        public static async Task SerializeRawDataIndex(IEnumerable<string> data, string filename)
        {
            using var file = File.OpenWrite(filename);
            file.SetLength(0);
            await JsonSerializer.SerializeAsync(file, data.Select((url, i) => new SerializedRawDataInfo(url, $"{i}.data")), _jsonOptions);
        }

        private class StringDiffConverter_ : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var (o, n) = ((string Old, string New))value;
                return $"{o} -> {n}";
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }

        private class SourceDiffConverter_ : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var (o, n) = ((Source Old, Source New))value;
                StringBuilder sb = new();
                if (o.Description == n.Description && o.Author == n.Author)
                {
                    sb.AppendLine($"{o.Description} ({o.Author})");
                }
                else
                {
                    sb.AppendLine($"{o.Description} -> {n.Description} ({o.Author} -> {n.Author})");
                }
                if (!o.Damage.Equals(n.Damage))
                {
                    sb.AppendLine($"伤害 {o.Damage} -> {n.Damage}");
                }
                if (!o.Comments.SequenceEqual(n.Comments))
                {
                    sb.AppendLine("备注修改");
                }
                if (!o.Links.SequenceEqual(n.Links))
                {
                    sb.AppendLine("链接修改");
                }
                if (!o.Images.SequenceEqual(n.Images))
                {
                    sb.AppendLine("图片修改");
                }
                return sb.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }

        public static readonly IValueConverter StringDiffConverter = new StringDiffConverter_();
        public static readonly IValueConverter SourceDiffConverter = new SourceDiffConverter_();
    }
}
