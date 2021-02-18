using System;

namespace BlogML2Hugo
{
    public interface IBlogUrlConverter
    {
        Uri GetPermalink(Uri url); 
        
        string GetSlug(Uri url);

        bool IsBlogUrl(Uri url);
    }
}