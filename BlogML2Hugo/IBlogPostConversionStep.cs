namespace BlogML2Hugo
{
    public interface IBlogPostConversionStep
    {
        void Execute(PostConversionData postConversionData);
    }
}
