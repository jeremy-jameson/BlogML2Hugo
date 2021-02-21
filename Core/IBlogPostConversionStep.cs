namespace BlogML2Hugo.Core
{
    public interface IBlogPostConversionStep
    {
        void Execute(BlogPostConversionData postConversionData);
    }
}
