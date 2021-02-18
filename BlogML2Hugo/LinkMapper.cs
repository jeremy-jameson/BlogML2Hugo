using System;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class LinkMapper
    {
        private readonly IBlogUrlConverter _blogUrlConverter;

        private Dictionary<string, Uri> _permalinkMap = new Dictionary<string, Uri>();

        public LinkMapper(IBlogUrlConverter blogUrlConverter)
        {
            if (blogUrlConverter == null)
            {
                throw new ArgumentNullException("blogUrlConverter");
            }

            _blogUrlConverter = blogUrlConverter;
        }

        public void Add(Uri url)
        {
            var permalink = _blogUrlConverter.GetPermalink(url);

            var slug = _blogUrlConverter.GetSlug(url);

             _permalinkMap.Add(slug, permalink);
        }

        public Uri GetPermalink(Uri url)
        {
            var slug = _blogUrlConverter.GetSlug(url);

            Uri newUrl = null;
            
            if (_permalinkMap.TryGetValue(slug, out newUrl) == true)
            {
                return newUrl;
            }

            return _blogUrlConverter.GetPermalink(url);
        }

        public bool IsBlogUrl(Uri url)
        {
            return _blogUrlConverter.IsBlogUrl(url);
        }
    }
}
