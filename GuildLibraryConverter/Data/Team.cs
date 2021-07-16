using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.Data
{
    class Character
    {
        public int Id { get; set; }
        public List<string> Requirements { get; set; } = new();
        public static readonly IEqualityComparer<Character> IdComparer = new CharacterIdComparer();

        private class CharacterIdComparer : IEqualityComparer<Character>
        {
            public bool Equals(Character x, Character y)
            {
                return x.Id == y.Id;
            }

            public int GetHashCode([DisallowNull] Character obj)
            {
                throw new NotImplementedException();
            }
        }
    }

    class Range : IEquatable<Range>
    {
        public long? Value { get; init; }
        public long? Low { get; init; }
        public long? High { get; init; }

        public override bool Equals(object obj)
        {
            return Equals(obj as Range);
        }

        public bool Equals(Range other)
        {
            return other != null &&
                   Value == other.Value &&
                   Low == other.Low &&
                   High == other.High;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value, Low, High);
        }

        public static bool operator ==(Range left, Range right)
        {
            return EqualityComparer<Range>.Default.Equals(left, right);
        }

        public static bool operator !=(Range left, Range right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            if (Value.HasValue)
            {
                return Value.ToString();
            }
            else
            {
                return $"{Low}-{High}";
            }
        }
    }

    class Source : IEquatable<Source>
    {
        public string Description { get; set; }
        public string Author { get; set; }
        public Range Damage { get; set; }
        public List<string> Links { get; set; } = new();
        public List<string> Images { get; set; } = new();
        public List<string> Comments { get; set; } = new();

        public override bool Equals(object obj)
        {
            return Equals(obj as Source);
        }

        public bool Equals(Source other)
        {
            return other != null &&
                   Description == other.Description &&
                   Author == other.Author &&
                   EqualityComparer<Range>.Default.Equals(Damage, other.Damage) &&
                   Links.SequenceEqual(other.Links) &&
                   Images.SequenceEqual(other.Images) &&
                   Comments.SequenceEqual(other.Comments);
        }

        public override int GetHashCode()
        {
            //Don't include the lists. We can tolerate hash collision, but need to ensure "equal" instances
            //have identical hash.
            return HashCode.Combine(Description, Author, Damage);
        }

        public static bool operator ==(Source left, Source right)
        {
            return EqualityComparer<Source>.Default.Equals(left, right);
        }

        public static bool operator !=(Source left, Source right)
        {
            return !(left == right);
        }
    }

    class Team
    {
        public string Boss { get; set; }
        public string Id { get; set; }
        public List<Character> Characters { get; set; } = new();
        public Range Time { get; set; }

        public List<string> Comments { get; set; } = new();
        public List<string> Labels { get; set; } = new();
        public long StandardDamage { get; set; }

        public List<Source> Sources { get; set; } = new();
    }

    class ListDiff<T, TDiff>
    {
        public List<T> Added { get; set; } = new();
        public List<T> Removed { get; set; } = new();
        public List<TDiff> Modified { get; set; } = new();

        public bool Any => Added.Count > 0 || Removed.Count > 0 || Modified.Count > 0;
    }

    class TeamDiff
    {
        public Team NewValue { get; set; }
        public Range TimeDiff { get; set; }
        public ListDiff<string, (string Old, string New)> CommentsDiff { get; set; } = new();
        public ListDiff<string, (string Old, string New)> LabelsDiff { get; set; } = new();
        public long? StandardDamageDiff { get; set; }
        public ListDiff<Source, (Source Old, Source New)> SourcesDiff { get; set; } = new();

        public bool Any => TimeDiff is not null ||
            CommentsDiff.Any || LabelsDiff.Any || StandardDamageDiff.HasValue || SourcesDiff.Any;
    }
}
