using BlogML.Xml;
using Html2Markdown;

namespace BlogML2Hugo
{
    public class Html2MarkdownConversionStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var mdConverter = new Converter();

            var markdown = mdConverter.Convert(
                post.Content.UncodedText);

            postConversionData.Post.Content = BlogMLContent.Create(
                markdown,
                BlogML.ContentTypes.Text);
        }
    }
}
