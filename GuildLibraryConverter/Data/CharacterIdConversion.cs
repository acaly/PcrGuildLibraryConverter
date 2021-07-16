using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.Data
{
    static class CharacterIdConversion
    {
        private static readonly Dictionary<string, int> _dictionary = new();

        static CharacterIdConversion()
        {
            using var reader = new StreamReader("CharacterInfo.csv");
            reader.ReadLine();
            while (!reader.EndOfStream)
            {
                var str = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(str)) continue;
                var fields = str.Split(',');
                var id = int.Parse(fields[0]);
                _dictionary[fields[4]] = id;
            }
        }

        public static int GetCharacterId(string text)
        {
            return _dictionary.TryGetValue(text, out var ret) ? ret : 0;
        }
    }
}
