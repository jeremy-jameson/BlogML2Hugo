using BlogML;
using BlogML.Xml;

namespace BlogML2Hugo.Core
{
    public class ReverseMarkdownDecodingStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var markdown = post.Content.Text;

            markdown = ReverseMarkdownHelper
                .DecodeAfterConversion(markdown);

            post.Content = BlogMLContent.Create(
                markdown,
                ContentTypes.Text);
        }
    }
}
