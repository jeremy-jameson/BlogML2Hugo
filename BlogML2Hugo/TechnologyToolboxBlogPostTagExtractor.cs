using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace BlogML2Hugo
{
    class TechnologyToolboxBlogPostTagExtractor : BlogPostTagExtractor
    {
        public TechnologyToolboxBlogPostTagExtractor(XmlDocument blogMLDoc) :
            base(blogMLDoc)
        {
        }

        public override IEnumerable<string> GetTags(BlogMLPost post)
        {
            var tags = base.GetTags(post);

            if (tags.Any() == false)
            {
                var postHtml = post.Content.UncodedText;

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(postHtml);
                {
                    tags = GetTagsFromPostContent(htmlDoc);
                    RemoveTagsFromPostContent(htmlDoc);
                }

                postHtml = htmlDoc.DocumentNode.OuterHtml;

                post.Content = BlogMLContent.Create(postHtml, ContentTypes.Html);
            }

            return tags;
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

        private static void RemoveTagsFromPostContent(HtmlDocument doc)
        {
            // Removes blog post content similar to the following:
            //
            //    <h3>
            //      Tags</h3>
            //    <ul>
            //      <li><a href="..." rel="tag">My System</a></li>
            //      <li><a href="..." rel="tag">Toolbox</a></li>
            //    </ul>

            var tagsList = doc.DocumentNode.SelectSingleNode(
                "//h3[normalize-space() = 'Tags']/following-sibling::ul");

            if (tagsList != null)
            {
                tagsList.Remove();
            }

            var tagsHeading = doc.DocumentNode.SelectSingleNode(
                "//h3[normalize-space() = 'Tags']");

            if (tagsHeading != null)
            {
                tagsHeading.Remove();
            }
        }
    }
}
