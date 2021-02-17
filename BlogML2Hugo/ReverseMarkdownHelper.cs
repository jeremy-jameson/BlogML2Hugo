using System.Collections.Generic;

namespace BlogML2Hugo
{
    /// <summary>
    /// This class is used to preserve underscores in Hugo shortcode parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Underscores are escaped by ReverseMarkdown during the HTML to Markdown
    /// conversion (specifically, the ReverseMarkdown.Converters.Text class
    /// replaces "_" with @"\"" in all #text nodes). This can result in invalid
    /// URLs in Hugo shortcode parameters or incorrectly modified content.
    /// </para>
    /// <para>
    /// For example:
    /// </para>
    /// <para>
    ///     {{< reference title="...and the WSS%5FWPG Group"... >}}
    /// </para>
    /// <para>
    /// instead of:
    /// </para>
    /// <para>
    ///     {{< reference title="...and the WSS_WPG Group"... >}}
    /// </para>
    /// <para>
    /// To avoid these issues, a custom encoding/decoding process is used.
    /// Underscores are encoded before the HTML to Markdown conversion -- using
    /// a custom token that is not expected to be encountered in any content --
    /// and the converted Markdown is subsequently decoded after the conversion
    /// is complete.
    /// </para>
    /// </remarks>
    public static class ReverseMarkdownHelper
    {
        private static readonly Dictionary<string, string> __escapedKeyChars =
            new Dictionary<string, string>();

        static ReverseMarkdownHelper()
        {
            __escapedKeyChars.Add("_", "{{%5F}}");
        }

        public static string DecodeAfterConversion(
            string markdown)
        {
            var decodedText = markdown;

            foreach (var item in __escapedKeyChars)
            {
                decodedText = decodedText.Replace(item.Value, item.Key);
            }

            return decodedText;
        }

        public static string EncodeBeforeConversion(
            string text)
        {
            var encodedText = text;

            foreach (var item in __escapedKeyChars)
            {
                encodedText = encodedText.Replace(item.Key, item.Value);
            }

            return encodedText;
        }
    }
}
