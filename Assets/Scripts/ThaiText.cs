using System.Text;

namespace EmoRelief
{
    /// <summary>
    /// Thai text support for IMGUI, which draws glyphs without OpenType shaping.
    /// Shape() swaps marks for the positional variants baked into EmoThai-Regular.ttf
    /// (see Tools/build_thai_font.py), and the line-break helpers let UiKit wrap Thai,
    /// which has no spaces between words, without splitting a syllable cluster.
    /// </summary>
    public static class ThaiText
    {
        // Word boundary marker inserted by the Python server (pythainlp). Never drawn.
        public const char WordBreak = '\u200B';

        private const int ToneHigh = 0xE000;
        private const int ToneHighLeft = 0xE010;
        private const int ToneLowLeft = 0xE020;
        private const int UpperLeft = 0xE030;

        public static bool HasThai(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var c in value)
                if (IsThai(c)) return true;
            return false;
        }

        public static bool IsThai(char c) => (c >= '\u0E00' && c <= '\u0E7F') || (c >= '\uE000' && c <= '\uE043');

        /// <summary>
        /// Set by UiKit once the bundled font is confirmed to contain the positional glyphs.
        /// While false, Shape() leaves text untouched so a missing font can never produce □ boxes.
        /// </summary>
        public static bool PositionalGlyphs { get; set; }

        public static string Shape(string value)
        {
            if (!PositionalGlyphs || !HasThai(value)) return value;
            var output = new StringBuilder(value);
            var baseIndex = -1;
            var hasUpper = false;
            for (var index = 0; index < value.Length; index++)
            {
                var c = value[index];
                if (c >= '\u0E01' && c <= '\u0E2E')
                {
                    baseIndex = index;
                    hasUpper = false;
                    continue;
                }
                var ascender = baseIndex >= 0 && IsAscender(value[baseIndex]);
                var upper = UpperSlot(c);
                if (upper >= 0)
                {
                    hasUpper = true;
                    if (ascender) output[index] = (char)(UpperLeft + upper);
                }
                else if (c >= '\u0E48' && c <= '\u0E4C')
                {
                    var tone = c - '\u0E48';
                    // Sara Am carries a nikhahit above, so the tone stacks over it too (น้ำ).
                    var high = hasUpper || (index + 1 < value.Length && value[index + 1] == '\u0E33');
                    if (high) output[index] = (char)((ascender ? ToneHighLeft : ToneHigh) + tone);
                    else if (ascender) output[index] = (char)(ToneLowLeft + tone);
                }
                else if (c >= '\u0E38' && c <= '\u0E3A' && baseIndex >= 0)
                {
                    var trimmed = TrimmedBase(value[baseIndex]);
                    if (trimmed != '\0') output[baseIndex] = trimmed;
                }
            }
            return output.ToString();
        }

        public static string StripBreaks(string value) =>
            value != null && value.IndexOf(WordBreak) >= 0 ? value.Replace(WordBreak.ToString(), "") : value;

        /// <summary>Characters that must stay attached to the character before them.</summary>
        public static bool CannotStartLine(char c)
        {
            if (c == '\u0E31' || (c >= '\u0E34' && c <= '\u0E3A') || (c >= '\u0E47' && c <= '\u0E4E')) return true;
            if (c >= '\uE000' && c <= '\uE036') return true;
            if (c == '\u0E30' || c == '\u0E32' || c == '\u0E33' || c == '\u0E45' || c == '\u0E46') return true;
            return c == ',' || c == '.' || c == '!' || c == '?' || c == ':' || c == ';' || c == ')' || c == '"' || c == '\'';
        }

        /// <summary>True when a line may break between <paramref name="previous"/> and <paramref name="next"/>.</summary>
        public static bool CanBreakBetween(char previous, char next)
        {
            if (CannotStartLine(next)) return false;
            if (previous == '-' || previous == '/') return true;
            var thaiBefore = IsThai(previous);
            var thaiAfter = IsThai(next);
            // A leading vowel always opens a new syllable (ความ|เครียด, แสดง|ให้|เห็น).
            if (thaiBefore && next >= '\u0E40' && next <= '\u0E44') return !(previous >= '\u0E40' && previous <= '\u0E44');
            return thaiBefore != thaiAfter && char.IsLetterOrDigit(previous) && char.IsLetterOrDigit(next);
        }

        private static bool IsAscender(char c) => c == '\u0E1B' || c == '\u0E1D' || c == '\u0E1F' || c == '\u0E2C';

        private static int UpperSlot(char c)
        {
            switch (c)
            {
                case '\u0E31': return 0;
                case '\u0E34': return 1;
                case '\u0E35': return 2;
                case '\u0E36': return 3;
                case '\u0E37': return 4;
                case '\u0E47': return 5;
                case '\u0E4D': return 6;
                default: return -1;
            }
        }

        private static char TrimmedBase(char c)
        {
            switch (c)
            {
                case '\u0E0E': return '\uE040';
                case '\u0E0F': return '\uE041';
                case '\u0E0D': return '\uE042';
                case '\u0E10': return '\uE043';
                default: return '\0';
            }
        }
    }
}
