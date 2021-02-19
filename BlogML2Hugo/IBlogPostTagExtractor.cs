using BlogML.Xml;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    interface IBlogPostTagExtractor
    {
        IEnumerable<string> GetTags(BlogMLPost post);
    }
}
