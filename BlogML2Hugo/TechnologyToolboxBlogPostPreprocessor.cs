using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System;

namespace BlogML2Hugo
{
    public class TechnologyToolboxBlogPostPreprocessor : IPostConversionStep
    {
        private readonly LinkMapper _linkMapper;

        public TechnologyToolboxBlogPostPreprocessor(LinkMapper linkMapper)
        {
            if (linkMapper == null)
            {
                throw new ArgumentNullException("linkMapper");
            }

            _linkMapper = linkMapper;
        }

        public void Execute(PostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            FixTechnologyToolboxBlogPostDates(post);

            var postHtml = post.Content.UncodedText;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(postHtml);
            {
                RemoveTagsFromPostContent(htmlDoc);
            }

            postHtml = htmlDoc.DocumentNode.OuterHtml;

            post.Content = BlogMLContent.Create(postHtml, ContentTypes.Html);

            var postUrl = new Uri(post.PostUrl);

            postConversionData.Slug = _linkMapper.GetSlug(postUrl);

            // Organize blog posts by year/month/day

            postConversionData.Subfolder =
                $"{post.DateCreated.ToLocalTime():yyyy-MM-dd}"
                .Replace("-", "\\");

            // Since the dates for blog posts have been updated -- for
            // example, to account for the migration from MSDN (Telligent)
            // to Technology Toolbox (Subtext) -- overwrite the post URLs
            // as necessary. In other words, Subtext didn't have the correct
            // dates for the blog posts migrated from MSDN, so the URLs
            // specified in the BlogML file are often "off" by a day.

            Uri url = new Uri(
                string.Concat(
                    "https://www.technologytoolbox.com/blog/jjameson/archive/",
                    postConversionData.Subfolder.Replace("\\", "/"),
                    "/",
                    postConversionData.Slug,
                    ".aspx"));

            postConversionData.Aliases.Add(postUrl.PathAndQuery);

            if (post.PostUrl != url.AbsoluteUri)
            {
                post.PostUrl = url.AbsoluteUri;

                postConversionData.Aliases.Add(url.PathAndQuery);
            }

            _linkMapper.Add(url);
        }

        private static void FixTechnologyToolboxBlogPostDates(BlogMLPost post)
        {
            // When migrating blog posts from MSDN (running Telligent) to
            // Technoogy Toolbox (running Subtext), the dates were offset
            // by a number of hours. Consequently the "day" portion of URLs
            // did not always match "date created" -- i.e. it could be off
            // by a day. This was likely due to the "lossy" date format
            // specified in the BlogML file used to export content from
            // Telligent into Subtext.
            //
            // In other words, all of the issues due to date/URL mismatch
            // seems to be due to dateCreated.ToString("s") -- since that
            // format does not preserve the timezone information. (Note
            // that Subtext suffers a similar issue in the BlogML export.)
            //
            // To resolve these issues (hopefully for the last time),
            // convert "Unspecified" DateTime values into "Local" DateTime
            // equivalents. However, note that blog posts created in
            // Subtext (i.e. not migrated from MSDN/Telligent) already
            // have the correct timestamp values and therefore should simply
            // be "forced" from "Unspecified" to "Local" without adjusting
            // the DateTime values.

            var msdnBlogCutoverDate = new DateTime(2011, 9, 3);

            if (post.DateCreated.Kind == DateTimeKind.Unspecified)
            {
                var offset = TimeZoneInfo.Local.GetUtcOffset(post.DateCreated);

                if (post.DateCreated < msdnBlogCutoverDate)
                {
                    post.DateCreated = post.DateCreated.Add(-offset);
                }

                post.DateCreated = DateTime.SpecifyKind(
                    post.DateCreated,
                    DateTimeKind.Local);
            }

            if (post.DateModified.Kind == DateTimeKind.Unspecified)
            {
                var offset = TimeZoneInfo.Local.GetUtcOffset(post.DateCreated);

                if (post.DateModified < msdnBlogCutoverDate)
                {
                    post.DateModified = post.DateModified.Add(-offset);
                }

                post.DateModified = DateTime.SpecifyKind(
                    post.DateModified,
                    DateTimeKind.Local);
            }
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
