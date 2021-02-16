using HtmlAgilityPack;
using System;
using System.Diagnostics;
using System.Linq;

namespace BlogML2Hugo
{
    public static class HtmlDocumentHelper
    {
        public static string NormalizeWhitespace(
            string html)
        {
            var normalizedText = html
                .Replace(Environment.NewLine, " ")
                .Replace("\t", " ");

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
