using HtmlAgilityPack;
using System;

namespace BlogML2Hugo.Core
{
    public class HugoShortcodeNodeBuilder
    {
        private HtmlDocument _htmlDoc = null;

        private string _htmlNodeName = "#text";

        private HugoShortcode _shortcode = null;

        public HtmlNode Build()
        {
            EnsureHtmlDocumentSpecified();
            EnsureNameSpecified();

            var shortcodeContent = _shortcode.ToString();

            if (this._htmlNodeName == "#text")
            {
                var shortcodeNode = _htmlDoc.CreateTextNode(shortcodeContent);

                return shortcodeNode;
            }
            else
            {
                var shortcodeNode = _htmlDoc.CreateElement(_htmlNodeName);

                shortcodeNode.AppendChild(
                    _htmlDoc.CreateTextNode(shortcodeContent));

                return shortcodeNode;
            }
        }

        private void EnsureHtmlDocumentSpecified()
        {
            if (_htmlDoc == null)
            {
                throw new InvalidOperationException(
                    "The HTML document must be specified.");
            }
        }

        private void EnsureNameSpecified()
        {
            if (_shortcode == null)
            {
                throw new InvalidOperationException(
                    "The shortcode name must be specified.");
            }
        }

        public HugoShortcodeNodeBuilder ForHtmlDocument(
            HtmlDocument htmlDoc)
        {
            _htmlDoc = htmlDoc;

            return this;
        }

        public HugoShortcodeNodeBuilder WithHtmlNodeName(
            string htmlNodeName)
        {
            if (htmlNodeName == null)
            {
                throw new ArgumentNullException("htmlNodeName");
            }

            if (string.IsNullOrWhiteSpace(htmlNodeName) == true)
            {
                throw new ArgumentException(
                    "HTML node name must be specified.",
                    "htmlNodeName");
            }

            _htmlNodeName = htmlNodeName;

            return this;
        }

        public HugoShortcodeNodeBuilder WithName(string shortcodeName)
        {
            _shortcode = new HugoShortcode(shortcodeName);

            return this;
        }

        public HugoShortcodeNodeBuilder WithParameter(
            string parameterValue)
        {
            EnsureNameSpecified();

            _shortcode.PositionalParameters.Add(
                parameterValue);

            return this;
        }

        public HugoShortcodeNodeBuilder WithParameter(
            string parameterName,
            string parameterValue)
        {
            EnsureNameSpecified();

            _shortcode.NamedParameters.Add(
                parameterName,
                parameterValue);

            return this;
        }

        public HugoShortcodeNodeBuilder WithParametersOnSeparateLines()
        {
            EnsureNameSpecified();

            _shortcode.WriteParametersOnSeparateLines = true;

            return this;
        }
    }
}
