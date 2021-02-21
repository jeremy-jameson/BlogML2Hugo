namespace BlogML2Hugo
{
    public interface IBlogPostConversionStep
    {
        void Execute(BlogPostConversionData postConversionData);
    }
}
