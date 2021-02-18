using System;
using System.Collections.Generic;
using System.Diagnostics;

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

            Uri mappedPermalink = null;

            if (_permalinkMap.TryGetValue(slug, out mappedPermalink) == true)
            {
                if (permalink.AbsoluteUri != mappedPermalink.AbsoluteUri)
                {
                    throw new ArgumentException(
                        $"Cannot add URL ({url} --> {permalink})"
                        + " because the slug ({slug})"
                        + " is already mapped to a different permalink"
                        + $" ({mappedPermalink}).");
                }
            }
            else
            {
                _permalinkMap.Add(slug, permalink);
            }
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
