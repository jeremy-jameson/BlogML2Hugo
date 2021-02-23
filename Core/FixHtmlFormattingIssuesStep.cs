using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Linq;

namespace BlogML2Hugo.Core
{
    public class FixHtmlFormattingIssuesStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var postHtml = post.Content.UncodedText;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(postHtml);

            FixSpacesInEmphasisElements(htmlDoc);

            FixSpacesInTableCells(htmlDoc);

            post.Content = BlogMLContent.Create(
                htmlDoc.DocumentNode.OuterHtml,
                ContentTypes.Html);
        }

        private static void FixSpacesInEmphasisElements(HtmlDocument doc)
        {
            // Replaces HTML content similar to the following:
            //
            //   ...click the <b>Advanced </b>button...
            //
            // with:
            //
            //   ...click the <b>Advanced</b> button...

            FixSpacesInEmphasisElements(
                doc,
                "//b[text() != normalize-space()]");

            FixSpacesInEmphasisElements(
                doc,
                "//em[text() != normalize-space()]");

            FixSpacesInEmphasisElements(
                doc,
                "//i[text() != normalize-space()]");

            FixSpacesInEmphasisElements(
                doc,
                "//strong[text() != normalize-space()]");
        }

        private static void FixSpacesInEmphasisElements(
            HtmlDocument doc,
            string xpath)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);

            if (nodes != null)
            {
                FixSpacesInEmphasisElements(nodes);
            }
        }

        private static void FixSpacesInEmphasisElements(
            HtmlNodeCollection nodes)
        {
            // Replaces HTML content similar to the following:
            //
            //   ...click the<b> Advanced </b>button...
            //
            // with:
            //
            //   ...click the <b>Advanced</b> button...

            foreach (var node in nodes)
            {
                Debug.Assert(
                    node.Name == "b"
                    || node.Name == "em"
                    || node.Name == "i"
                    || node.Name == "strong");

                var emphasisNode = node;

                if (emphasisNode.FirstChild != null
                    && emphasisNode.FirstChild.Name == "#text")
                {
                    var child = emphasisNode.FirstChild;

                    var trimmedText = child.InnerText.TrimStart();

                    if (trimmedText != child.InnerText)
                    {
                        HtmlDocumentHelper.EnsureWhitespaceBeforeElement(
                            emphasisNode);

                        child.InnerHtml = trimmedText;
                    }
                }

                if (emphasisNode.LastChild != null
                    && emphasisNode.LastChild.Name == "#text")
                {
                    var child = emphasisNode.LastChild;

                    var trimmedText = child.InnerText.TrimEnd();

                    if (trimmedText != child.InnerText)
                    {
                        HtmlDocumentHelper.EnsureWhitespaceAfterElement(
                            emphasisNode);

                        child.InnerHtml = trimmedText;
                    }
                }
            }
        }

        private static void FixSpacesInTableCells(HtmlDocument doc)
        {
            // Normalize whitespace in "simple" <td> content to fix a number of
            // issues during the Markdown conversion process
            //
            // For example, the line breaks in the last column of the table in
            // the following blog post result in "corruption" when converting
            // from HTML to Markdown:
            //
            // https://www.technologytoolbox.com/blog/jjameson/archive/2012/02/19/html-to-pdf-converters.aspx

            var elements = doc.DocumentNode.SelectNodes("//td");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    // Ignore table cells containing HTML line breaks and other
                    // "block" content (e.g. lists)

                    if (element.Descendants("br").Any() == false
                        && element.Descendants("ol").Any() == false
                        && element.Descendants("ul").Any() == false)
                    {
                        HtmlDocumentHelper.NormalizeWhitespaceInChildTextNodes(
                            element);
                    }
                }
            }
        }
    }
}
