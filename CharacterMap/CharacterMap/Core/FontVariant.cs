﻿using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Storage;

namespace CharacterMap.Core
{
    [System.Diagnostics.DebuggerDisplay("{FamilyName} {PreferredName}")]
    public partial class FontVariant : IDisposable
    {
        /* Using a character cache avoids a lot of unnecessary allocations */
        private static Dictionary<int, Character> _characters { get; } = new Dictionary<int, Character>();

        private IReadOnlyList<KeyValuePair<string, string>> _fontInformation = null;
        private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures = null;
        private IReadOnlyList<TypographyFeatureInfo> _xamlTypographyFeatures = null;
        private FontAnalysis _analysis = null;

        public IReadOnlyList<KeyValuePair<string, string>> FontInformation
            => _fontInformation ??= LoadFontInformation();

        public IReadOnlyList<TypographyFeatureInfo> TypographyFeatures
        {
            get
            {
                if (_typographyFeatures == null)
                    LoadTypographyFeatures();
                return _typographyFeatures;
            }
        }

        /// <summary>
        /// Supported XAML typographer features for A SINGLE GLYPH. 
        /// Does not include features like Alternates which are used for strings of text.
        /// </summary>
        public IReadOnlyList<TypographyFeatureInfo> XamlTypographyFeatures
        {
            get
            {
                if (_xamlTypographyFeatures == null)
                    LoadTypographyFeatures();
                return _xamlTypographyFeatures;
            }
        }

        public bool HasXamlTypographyFeatures => XamlTypographyFeatures.Count > 0;

        public CanvasFontFace FontFace { get; private set; }

        public string PreferredName { get; private set; }

        public IReadOnlyList<Character> Characters { get; private set; }

        public double CharacterHash { get; private set; }

        public bool IsImported { get; }

        public string FileName { get; }

        public string FamilyName { get; }

        public string FamilyNameWin2D { get; }

        public CanvasUnicodeRange[] UnicodeRanges => FontFace.UnicodeRanges;

        public Panose Panose { get; }

        public DWriteProperties DirectWriteProperties { get; }

        /// <summary>
        /// File-system path for XAML to construct a font for use in this application
        /// </summary>
        public string Source { get; }
        
        /// <summary>
        /// Source for DirectWrite to construct a font from. 
        /// Whereas Source on Windows 11 uses Typographic family names,
        /// our DWrite control relies on using the WSS family name on both
        /// Windows 10 and Windows 11 SDK's
        /// </summary>
        public string WSSSource => DirectWriteProperties.FamilyName;

        /// <summary>
        /// A FontFamily source for XAML that includes a custom fallback font.
        /// This results in XAML *only* rendering the characters included in the font.
        /// Use when you may have a scenario where characters not inside a font's glyph
        /// range might be displayed, otherwise use <see cref="Source"/> for better performance.
        /// </summary>
        public string DisplaySource => $"{Source}, /Assets/AdobeBlank.otf#Adobe Blank";

        public string XamlFontSource =>
            (IsImported ? $"/Assets/Fonts/{FileName}#{FamilyName}" : Source);

        public FontVariant(CanvasFontFace face, StorageFile file, DWriteProperties dwProps)
        {
            FontFace = face;
            FamilyName = Utils.IsWindows11SDK ? dwProps.TypographicFamilyName : dwProps.FamilyName;

            if (file != null)
            {
                IsImported = true;
                FileName = file.Name;
                Source = $"{FontFinder.GetAppPath(file)}#{FamilyName}";
            }
            else
            {
                Source = FamilyName;
            }

            string name = dwProps.FaceName;
            if (String.IsNullOrEmpty(name))
                name = Utils.GetVariantDescription(face);

            DirectWriteProperties = dwProps;
            PreferredName = name;
            Panose = PanoseParser.Parse(face);

            FamilyNameWin2D = FontFace.FamilyNames["en-us"];
        }

        public string GetProviderName()
        {
            //if (!String.IsNullOrEmpty(DirectWriteProperties.RemoteProviderName))
            //    return DirectWriteProperties.RemoteProviderName;

            if (IsImported)
                return Localization.Get("InstallTypeImported");

            return Localization.Get($"DWriteSource{DirectWriteProperties.Source}");
        }

        public IReadOnlyList<Character> GetCharacters()
        {
            if (Characters == null)
            {
                var characters = new List<Character>();
                foreach (var range in FontFace.UnicodeRanges)
                {
                    CharacterHash += range.First;
                    CharacterHash += range.Last;

                    int last = (int)range.Last;
                    for (int i = (int)range.First; i <= last; i++)
                    {
                        if (!_characters.TryGetValue(i, out Character c))
                        {
                            c = new Character((uint)i);
                            _characters[i] = c;
                        }

                        characters.Add(c);
                    }
                }
                Characters = characters;
            }

            return Characters;
        }

        public int GetGlyphIndex(Character c)
        {
            int[] results = FontFace.GetGlyphIndices(new uint[] { c.UnicodeIndex });
            return results[0];
        }

        public uint[] GetGlyphUnicodeIndexes()
        {
            return GetCharacters().Select(c => c.UnicodeIndex).ToArray();
        }

        public FontAnalysis GetAnalysis()
        {
            return _analysis ??= TypographyAnalyzer.Analyze(this);
        }

        public string TryGetSampleText()
        {
            return GetInfoKey(FontFace, CanvasFontInformation.SampleText).Value;
        }

        private void LoadTypographyFeatures()
        {
            var features = TypographyAnalyzer.GetSupportedTypographyFeatures(this);

            var xaml = features.Where(f => TypographyBehavior.IsXamlSingleGlyphSupported(f.Feature)).ToList();
            if (xaml.Count > 0)
                xaml.Insert(0, TypographyFeatureInfo.None);
            _xamlTypographyFeatures = xaml;

            if (features.Count > 0)
                features.Insert(0, TypographyFeatureInfo.None);
            _typographyFeatures = features;
        }

        private List<KeyValuePair<string, string>> LoadFontInformation()
        {
            //KeyValuePair<string, string> Get(CanvasFontInformation info)
            //{
            //    var infos = FontFace.GetInformationalStrings(info);
            //    if (infos.Count == 0)
            //        return new KeyValuePair<string, string>();

            //    var name = info.Humanise();
            //    var dic = infos.ToDictionary(k => k.Key, k => k.Value);
            //    if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
            //        || infos.TryGetValue("en-us", out value))
            //        return KeyValuePair.Create(name, value);
            //    return KeyValuePair.Create(name, infos.First().Value);
            //}

            return INFORMATIONS.Select(i => GetInfoKey(FontFace, i)).Where(s => s.Key != null).ToList();
        }

        private static KeyValuePair<string, string> GetInfoKey(CanvasFontFace fontFace, CanvasFontInformation info)
        {
            var infos = fontFace.GetInformationalStrings(info);
            if (infos.Count == 0)
                return new KeyValuePair<string, string>();

            var name = info.Humanise();
            var dic = infos.ToDictionary(k => k.Key, k => k.Value);
            if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
                || infos.TryGetValue("en-us", out value))
                return KeyValuePair.Create(name, value);
            return KeyValuePair.Create(name, infos.First().Value);
        }




        /* SEARCHING */

        public Dictionary<Character, string> SearchMap { get; set; }

        public string GetDescription(Character c)
        {
            if (SearchMap == null 
                || !SearchMap.TryGetValue(c, out string mapping)
                || string.IsNullOrWhiteSpace(mapping))
                return GlyphService.GetCharacterDescription(c.UnicodeIndex, this);

            return GlyphService.TryGetAGLFNName(mapping);
        }




        /* .NET */

        public void Dispose()
        {
            FontFace.Dispose();
            FontFace = null;
        }

        public override string ToString()
        {
            return PreferredName;
        }
    }


    public partial class FontVariant
    {
        public static FontVariant CreateDefault(CanvasFontFace face)
        {
            return new FontVariant(face, null, DWriteProperties.CreateDefault())
            {
                PreferredName = "",
                Characters = new List<Character>
                {
                    new Character(0)
                }
            };
        }

        private static CanvasFontInformation[] INFORMATIONS { get; } = {
            CanvasFontInformation.FullName,
            CanvasFontInformation.Description,
            CanvasFontInformation.Designer,
            CanvasFontInformation.DesignerUrl,
            CanvasFontInformation.VersionStrings,
            CanvasFontInformation.FontVendorUrl,
            CanvasFontInformation.Manufacturer,
            CanvasFontInformation.Trademark,
            CanvasFontInformation.CopyrightNotice,
            CanvasFontInformation.LicenseInfoUrl,
            CanvasFontInformation.LicenseDescription,
        };
    }
}
