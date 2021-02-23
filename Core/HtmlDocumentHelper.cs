using HtmlAgilityPack;
using System;
using System.Diagnostics;
using System.Linq;

namespace BlogML2Hugo.Core
{
    public static class HtmlDocumentHelper
    {
        public static void EnsureWhitespaceAfterElement(
            HtmlNode element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            HtmlNode whitespaceTextNode = null;

            if (element.NextSibling != null
                && element.NextSibling.Name == "#text")
            {
                var textNode = element.NextSibling;

                if (textNode.InnerText.TrimStart()
                    != textNode.InnerText)
                {
                    whitespaceTextNode = textNode;
                }
            }

            if (whitespaceTextNode == null)
            {
                whitespaceTextNode = element.OwnerDocument
                    .CreateTextNode(" ");

                element.ParentNode.InsertAfter(
                    whitespaceTextNode,
                    element);
            }
        }
        
        public static void EnsureWhitespaceBeforeElement(
            HtmlNode element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            HtmlNode whitespaceTextNode = null;

            if (element.PreviousSibling != null
                && element.PreviousSibling.Name == "#text")
            {
                var textNode = element.PreviousSibling;

                if (textNode.InnerText.TrimEnd()
                    != textNode.InnerText)
                {
                    whitespaceTextNode = textNode;
                }
            }

            if (whitespaceTextNode == null)
            {
                whitespaceTextNode = element.OwnerDocument
                    .CreateTextNode(" ");

                element.ParentNode.InsertBefore(
                    whitespaceTextNode,
                    element);
            }
        }

        public static void MoveLeadingWhitespaceToParent(
            HtmlNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            if (node.FirstChild != null
                && node.FirstChild.Name == "#text")
            {
                var textNode = node.FirstChild;

                var trimmedText = textNode.InnerText.TrimStart();

                if (trimmedText != textNode.InnerText)
                {
                    HtmlDocumentHelper.EnsureWhitespaceBeforeElement(
                        node);

                    textNode.InnerHtml = trimmedText;
                }
            }
        }

        public static void MoveTrailingWhitespaceToParent(
            HtmlNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            if (node.LastChild != null
                && node.LastChild.Name == "#text")
            {
                var textNode = node.LastChild;

                var trimmedText = textNode.InnerText.TrimEnd();

                if (trimmedText != textNode.InnerText)
                {
                    HtmlDocumentHelper.EnsureWhitespaceAfterElement(
                        node);

                    textNode.InnerHtml = trimmedText;
                }
            }
        }

        public static string NormalizeWhitespace(
            string html)
        {
            var normalizedText = html
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ');

            while (normalizedText.IndexOf("  ") != -1)
            {
                normalizedText = normalizedText.Replace("  ", " ");
            }

            Debug.Assert(normalizedText.IndexOf("  ") == -1);
            return normalizedText;
        }

        public static void NormalizeWhitespaceInChildTextNodes(HtmlNode node)
        {
            // Replaces HTML content similar to the following:
            //
            //   <p>Some     <i>cool</i>                  <b>content</b>
            //     <a href="#">...</a>...
            //   </p>
            //
            // with:
            //
            //   <p>Some <i>cool</i> <b>content</b> <a href='#'>...</a>... </p>

            node.ChildNodes.ToList().ForEach((child) =>
            {
                if (child.Name == "#text")
                {
                    string normalizedText = NormalizeWhitespace(child.InnerHtml);

                    child.InnerHtml = normalizedText;
                }
            });
        }
    }
}
