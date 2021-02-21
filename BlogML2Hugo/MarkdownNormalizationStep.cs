using Markdig;

namespace BlogML2Hugo
{
    public class MarkdownNormalizationStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var markdown = Markdown.Normalize(postConversionData.Markdown);

            markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(
                markdown);

            markdown = ReverseMarkdownHelper.DecodeAfterConversion(
                markdown);

            postConversionData.Markdown = markdown;
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
