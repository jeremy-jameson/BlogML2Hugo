using BlogML;
using BlogML.Xml;
using Markdig;

namespace BlogML2Hugo
{
    public class MarkdownNormalizationStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var markdown = Markdown.Normalize(
                postConversionData.Post.Content.UncodedText);

            markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(
                markdown);

            markdown = ReverseMarkdownHelper.DecodeAfterConversion(
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
