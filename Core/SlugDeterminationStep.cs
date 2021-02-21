using System;

namespace BlogML2Hugo.Core
{
    public class SlugDeterminationStep : IBlogPostConversionStep
    {
        private readonly IBlogUrlConverter _blogUrlConverter;

        public SlugDeterminationStep(
            IBlogUrlConverter blogUrlConverter)
        {
            if (blogUrlConverter == null)
            {
                throw new ArgumentNullException("blogUrlConverter");
            }

            _blogUrlConverter = blogUrlConverter;
        }

        public void Execute(BlogPostConversionData postConversionData)
        {
            var postUrl = new Uri(postConversionData.Post.PostUrl);

            postConversionData.Slug = _blogUrlConverter.GetSlug(postUrl);
        }
    }
}
