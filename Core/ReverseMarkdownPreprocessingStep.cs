using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System.Diagnostics;

namespace BlogML2Hugo.Core
{
    public class ReverseMarkdownPreprocessingStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var postHtml = post.Content.UncodedText;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(postHtml);

            AddPlaceholderParagraphsForBlockquotesInLists(htmlDoc);
            EncodeSpecialCharactersInHugoShortcodes(htmlDoc);

            post.Content = BlogMLContent.Create(
                htmlDoc.DocumentNode.OuterHtml,
                ContentTypes.Html);
        }

        private void AddPlaceholderParagraphsForBlockquotesInLists(
            HtmlDocument doc)
        {
            var elements = doc.DocumentNode.SelectNodes("//li/blockquote");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    Debug.Assert(element.Name == "blockquote");
                    var blockquote = element;

                    var shortcode = new HugoShortcodeNodeBuilder()
                        .ForHtmlDocument(doc)
                        .WithHtmlNodeName("p")
                        .WithName("reverse-markdown-hack")
                        .Build();

                    blockquote.ParentNode.InsertAfter(
                        shortcode,
                        blockquote);
                }
            }
        }

        private void EncodeSpecialCharactersInHugoShortcodes(HtmlDocument doc)
        {
            // Find elements containing a Hugo shortcode (e.g.
            // "{{< figure... >}}"

            var elements = doc.DocumentNode.SelectNodes(
                "//*[contains(text(), '{{<')]/text()");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    Debug.Assert(element.Name == "#text");
                    var textNode = element;

                    textNode.InnerHtml = ReverseMarkdownHelper
                        .EncodeBeforeConversion(textNode.InnerHtml);
                }
            }
        }
    }
}
