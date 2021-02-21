using HtmlAgilityPack;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace BlogML2Hugo
{
    class TechnologyToolboxBlogPostTagExtractor : TagExtractionStep
    {
        public TechnologyToolboxBlogPostTagExtractor(XmlDocument blogMLDoc) :
            base(blogMLDoc)
        {
        }

        public override void Execute(BlogPostConversionData postConversionData)
        {
            base.Execute(postConversionData);

            if (postConversionData.Tags.Any() == false)
            {
                var postHtml = postConversionData.Post.Content.UncodedText;

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(postHtml);
                {
                    var tags = GetTagsFromPostContent(htmlDoc);

                    postConversionData.Tags.AddRange(tags);
                }
            }
        }

        private static List<string> GetTagsFromPostContent(HtmlDocument doc)
        {
            // Parse tags from the content of a blog post that contains HTML
            // similar to the following:
            //
            //    <h3>
            //      Tags</h3>
            //    <ul>
            //      <li><a href="..." rel="tag">My System</a></li>
            //      <li><a href="..." rel="tag">Toolbox</a></li>
            //    </ul>
            //
            // For the example HTML above, a list containing "My System" and
            // "Toolbox" would be returned.

            var tagLinks = doc.DocumentNode.SelectNodes(
                "//h3[normalize-space() = 'Tags']/following-sibling::ul/li/a");

            var tags = new List<string>();

            if (tagLinks != null)
            {
                foreach (var tagLink in tagLinks)
                {
                    tags.Add(tagLink.InnerText);
                }
            }

            return tags;
        }
    }
}
