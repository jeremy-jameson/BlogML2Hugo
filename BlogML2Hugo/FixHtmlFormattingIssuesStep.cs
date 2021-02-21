using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Linq;

namespace BlogML2Hugo
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

            ProcessBlogTableCells(htmlDoc);

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

                node.ChildNodes.ToList().ForEach((child) =>
                {
                    if (child.Name == "#text")
                    {
                        var trimmedText = child.InnerText.TrimStart();

                        if (trimmedText != child.InnerText)
                        {
                            var emphasisNode = child.ParentNode;
                            HtmlNode whitespaceTextNode = null;

                            if (emphasisNode.PreviousSibling != null
                                && emphasisNode.PreviousSibling.Name == "#text")
                            {
                                var textNode = emphasisNode.PreviousSibling;

                                if (textNode.InnerText.TrimEnd()
                                    != textNode.InnerText)
                                {
                                    whitespaceTextNode = textNode;
                                }
                            }

                            child.InnerHtml = trimmedText;

                            if (whitespaceTextNode == null)
                            {
                                whitespaceTextNode = node.OwnerDocument
                                    .CreateTextNode(" ");

                                node.ParentNode.InsertBefore(
                                    whitespaceTextNode,
                                    node);
                            }
                        }

                        trimmedText = child.InnerText.TrimEnd();

                        if (trimmedText != child.InnerText)
                        {
                            var emphasisNode = child.ParentNode;
                            HtmlNode whitespaceTextNode = null;

                            if (emphasisNode.NextSibling != null
                                && emphasisNode.NextSibling.Name == "#text")
                            {
                                var textNode = emphasisNode.NextSibling;

                                if (textNode.InnerText.TrimStart()
                                    != textNode.InnerText)
                                {
                                    whitespaceTextNode = textNode;
                                }
                            }

                            child.InnerHtml = trimmedText;

                            if (whitespaceTextNode == null)
                            {
                                whitespaceTextNode = node.OwnerDocument
                                    .CreateTextNode(" ");

                                node.ParentNode.InsertAfter(
                                    whitespaceTextNode,
                                    node);
                            }
                        }
                    }
                });
            }
        }

        private static void ProcessBlogTableCells(HtmlDocument doc)
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
