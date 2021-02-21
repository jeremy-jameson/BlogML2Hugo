using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

            FixSpacesInsideEmphasisElements(htmlDoc);

            post.Content = BlogMLContent.Create(
                htmlDoc.DocumentNode.OuterHtml,
                ContentTypes.Html);
        }

        private static void FixSpacesInsideEmphasisElements(HtmlDocument doc)
        {
            // Replaces HTML content similar to the following:
            //
            //   ...click the <b>Advanced </b>button...
            //
            // with:
            //
            //   ...click the <b>Advanced</b> button...

            FixSpacesInsideEmphasisElements(
                doc,
                "//b[text() != normalize-space()]");

            FixSpacesInsideEmphasisElements(
                doc,
                "//em[text() != normalize-space()]");

            FixSpacesInsideEmphasisElements(
                doc,
                "//i[text() != normalize-space()]");

            FixSpacesInsideEmphasisElements(
                doc,
                "//strong[text() != normalize-space()]");
        }

        private static void FixSpacesInsideEmphasisElements(
            HtmlDocument doc,
            string xpath)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);

            if (nodes != null)
            {
                FixSpacesInsideEmphasisElements(nodes);
            }
        }

        private static void FixSpacesInsideEmphasisElements(
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
    }
}
