using ReverseMarkdown;

namespace BlogML2Hugo
{
    public class MarkdownConversionStep : IBlogPostConversionStep
    {
        public void Execute(PostConversionData postConversionData)
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
