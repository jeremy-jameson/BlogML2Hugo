using BlogML;
using BlogML.Xml;
using Markdig;

namespace BlogML2Hugo.Core
{
    public class MarkdownNormalizationStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var markdown = postConversionData.Post.Content.UncodedText;

            markdown = ReverseMarkdownHelper.DecodeAfterConversion(
                markdown);

            markdown = Markdown.Normalize(markdown);

            markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(
                markdown);

            postConversionData.Post.Content = BlogMLContent.Create(
                markdown,
                ContentTypes.Text);
        }

        private static string RemoveTrailingSpacesFromEmptyBlockquoteLines(
            string markdown)
        {
            int index = markdown.IndexOf("\n" + "> " + "\n");

            if (index == -1)
            {
                return markdown;
            }

            return markdown.Replace("\n> \n", "\n>\n");
        }
    }
}
