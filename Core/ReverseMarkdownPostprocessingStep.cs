using BlogML;
using BlogML.Xml;
using System;
using System.Linq;

namespace BlogML2Hugo.Core
{
    public class ReverseMarkdownPostprocessingStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var markdown = post.Content.Text;

            markdown = ReverseMarkdownHelper
                .DecodeAfterConversion(markdown);

            markdown = RemovePlaceholderParagraphsForBlockquotesInLists(
                markdown);

            post.Content = BlogMLContent.Create(
                markdown,
                ContentTypes.Text);
        }

        private string RemovePlaceholderParagraphsForBlockquotesInLists(
            string markdown)
        {
            var filteredMarkdown = markdown.Split(
                new string[] { Environment.NewLine },
                StringSplitOptions.None)
                .ToList()
                .Where(x => x.Trim() != "{{< reverse-markdown-hack >}}")
                .ToArray();

            return string.Join(Environment.NewLine, filteredMarkdown);
        }
    }
}
