using System;
using System.IO;

namespace BlogML2Hugo
{
    public class TechnologyToolboxBlogUrlConverter : IBlogUrlConverter
    {
        private const string _host = "www.technologytoolbox.com";

        public Uri GetPermalink(Uri url)
        {
            string relativeUrl = GetRelativeUrl(url);

            if (relativeUrl.StartsWith(
                "/blog/jjameson/archive/",
                StringComparison.OrdinalIgnoreCase) == true)
            {
                relativeUrl = relativeUrl.Replace(
                    "/blog/jjameson/archive/",
                    "/blog/jjameson/");

                if (relativeUrl.EndsWith(
                    ".aspx",
                    StringComparison.OrdinalIgnoreCase) == true)
                {
                    relativeUrl = relativeUrl.Remove(
                        relativeUrl.Length - ".aspx".Length);
                }
            }

            Uri baseUrl = new Uri($"https://{_host}");

            return new Uri(baseUrl, relativeUrl);
        }

        private string GetRelativeUrl(Uri url)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            if (url.IsAbsoluteUri == true && url.Host != _host)
            {
                throw new ArgumentException(
                    $"URL ({url}) does not contain expected host ("
                        + _host + ").",
                    "url");
            }

            string relativeUrl = null;

            if (url.IsAbsoluteUri == true)
            {
                relativeUrl = url.PathAndQuery;
            }
            else
            {
                relativeUrl = url.ToString();
            }

            return relativeUrl;
        }

        public string GetSlug(Uri url)
        {
            string relativeUrl = GetRelativeUrl(url);

            var slug = Path.GetFileNameWithoutExtension(
                relativeUrl);

            return slug;
        }

        public bool IsBlogUrl(Uri url)
        {
            if (url.IsAbsoluteUri == true && url.Host != _host)
            {
                return false;
            }

            var relativeUrl = GetRelativeUrl(url);

            return relativeUrl.StartsWith(
                "/blog/jjameson",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
