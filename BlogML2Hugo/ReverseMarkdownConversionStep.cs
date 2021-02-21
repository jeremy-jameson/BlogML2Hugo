using BlogML.Xml;
using ReverseMarkdown;

namespace BlogML2Hugo
{
    public class ReverseMarkdownConversionStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var config = new ReverseMarkdown.Config
            {
                GithubFlavored = true,
            };

            var mdConverter = new Converter(config);

            var markdown = mdConverter.Convert(
                post.Content.UncodedText);

            postConversionData.Post.Content = BlogMLContent.Create(
                markdown,
                BlogML.ContentTypes.Text);
        }
    }
}
