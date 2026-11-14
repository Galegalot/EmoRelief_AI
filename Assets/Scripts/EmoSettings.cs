using UnityEngine;

namespace EmoRelief
{
    /// <summary>
    /// Player preferences that shape the AI request: output language and home location.
    /// </summary>
    public static class EmoSettings
    {
        public const string English = "en";
        public const string Thai = "th";
        public const string DefaultLocation = "Thailand";
        public const int MaxLocationLength = 40;

        private const string LanguageKey = "emo.language";
        private const string LocationKey = "emo.homeLocation";

        private static string language;
        private static string location;

        public static string Language
        {
            get => language ?? (language = PlayerPrefs.GetString(LanguageKey, English) == Thai ? Thai : English);
            set
            {
                language = value == Thai ? Thai : English;
                PlayerPrefs.SetString(LanguageKey, language);
                PlayerPrefs.Save();
            }
        }

        public static bool IsThai => Language == Thai;

        /// <summary>What the player typed (may be blank). Thai or English are both fine.</summary>
        public static string Location
        {
            get => location ?? (location = PlayerPrefs.GetString(LocationKey, ""));
            set
            {
                location = (value ?? "").Replace("\n", " ").Replace("\r", " ");
                if (location.Length > MaxLocationLength) location = location.Substring(0, MaxLocationLength);
                PlayerPrefs.SetString(LocationKey, location);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Location sent to the AI; a blank entry quietly means "somewhere in Thailand".</summary>
        public static string PayloadLocation(string typed, string forLanguage)
        {
            if (!string.IsNullOrWhiteSpace(typed)) return typed.Trim();
            return forLanguage == Thai ? "ประเทศไทย" : DefaultLocation;
        }

        public static string Pick(string english, string thai) => IsThai ? thai : english;
    }
}
