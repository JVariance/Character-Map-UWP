﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Storage;

namespace CharacterMap.Models
{
    public class UserFontCollection
    {
        public bool IsSystemSymbolCollection { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public HashSet<string> Fonts { get; set; } = new ();

        internal string GetFlatFonts()
        {
            return string.Join('', Fonts);
        }
    }

    public class SQLiteFontCollection
    { 
        public int Id { get; set; }
        public string Name { get; set; }
        public string Fonts { get; set; }

        public UserFontCollection AsUserFontCollection()
        {
            return new UserFontCollection
            {
                Id = Id,
                Name = Name,
                Fonts = new(Fonts.Split(''))
            };
        }
    }

}
