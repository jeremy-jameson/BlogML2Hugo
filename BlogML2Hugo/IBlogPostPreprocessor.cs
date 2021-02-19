using BlogML.Xml;

namespace BlogML2Hugo
{
    public interface IBlogPostPreprocessor
    {
        void Process(BlogMLPost post);
    }
}
