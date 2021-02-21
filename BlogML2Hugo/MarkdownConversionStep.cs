using ReverseMarkdown;

namespace BlogML2Hugo
{
    public class MarkdownConversionStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var config = new ReverseMarkdown.Config
            {
                GithubFlavored = true,
            };

            var mdConverter = new Converter(config);

            postConversionData.Markdown = mdConverter.Convert(
                post.Content.UncodedText);
        }
    }
}
