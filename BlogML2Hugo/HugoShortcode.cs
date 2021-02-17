using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace BlogML2Hugo
{
    public class HugoShortcode
    {
        public string Name { get; private set; }

        public NameValueCollection NamedParameters { get; private set; }

        public List<string> PositionalParameters { get; private set; }

        public bool WriteParametersOnSeparateLines { get; set; }

        public HugoShortcode(string shortcodeName)
        {
            if (shortcodeName == null)
            {
                throw new ArgumentNullException("shortcodeName");
            }

            if (string.IsNullOrWhiteSpace(shortcodeName) == true)
            {
                throw new ArgumentException(
                    "Shortcode name must be specified.",
                    "shortcodeName");
            }

            this.Name = shortcodeName.Trim();
            this.NamedParameters = new NameValueCollection();
            this.PositionalParameters = new List<string>();
        }

        private static void AppendHugoShortcodeParameterValue(
            StringBuilder sb,
            string parameterName,
            string parameterValue,
            bool omitIfNullOrWhitespace = true,
            bool appendNewLine = true)
        {
            if (omitIfNullOrWhitespace == true
                && string.IsNullOrWhiteSpace(parameterValue) == true)
            {
                return;
            }

            var encodedParameterValue =
                HtmlDocumentHelper.NormalizeWhitespace(parameterValue)
                .Replace("\"", "&quot;")
                .Replace("&quot;", "\\&quot;")
                .Trim();

            encodedParameterValue =
                ReverseMarkdownHelper.EncodeBeforeConversion(
                    encodedParameterValue);

            if (string.IsNullOrWhiteSpace(parameterName) == false)
            {
                sb.Append($" {parameterName}=\"{encodedParameterValue}\"");

            }
            else
            {
                sb.Append($" \"{encodedParameterValue}\"");
            }

            if (appendNewLine == true)
            {
                sb.AppendLine();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append("{{< ");
            sb.Append(this.Name);

            if (this.WriteParametersOnSeparateLines == true)
            {
                sb.AppendLine();
            }

            this.NamedParameters.AllKeys.ToList().ForEach(parameterName =>
            {
                var parameterValue = this.NamedParameters[parameterName];

                AppendHugoShortcodeParameterValue(
                    sb,
                    parameterName,
                    parameterValue,
                    appendNewLine: this.WriteParametersOnSeparateLines);
            });

            this.PositionalParameters.ToList().ForEach(parameterValue =>
            {
                AppendHugoShortcodeParameterValue(
                    sb,
                    null,
                    parameterValue,
                    appendNewLine: this.WriteParametersOnSeparateLines);
            });

            while (char.IsWhiteSpace(sb[sb.Length - 1]) == true)
            {
                sb.Remove(sb.Length - 1, 1);
            }

            sb.Append(" >}}");

            var shortcode = sb.ToString();

            return shortcode;
        }
    }
}
