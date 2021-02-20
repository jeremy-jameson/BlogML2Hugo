using System;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class TechnologyToolboxImageUrlMapper : IUrlMapper
    {
        private const string _newImageHost = "assets.technologytoolbox.com";
        private const string _oldImageHost = "www.technologytoolbox.com";

        private const string _newImageBasePath = "/blog/jjameson/Images";
        private const string _oldImageBasePath =
            "/blog/images/www_technologytoolbox_com/blog/jjameson";

        private Dictionary<string, Uri> _imageUrlMap = new Dictionary<string, Uri>();

        public TechnologyToolboxImageUrlMapper()
        {
            _imageUrlMap = new Dictionary<string, Uri>();

            AddMappedImageUrl("10/o_Taskbar-Shortcuts.jpg",
                "My-System/Taskbar-Shortcuts-301x96.jpg");

            AddMappedImageUrl("9/r_SharePoint-Designer-Error-Creating-Control.png",
                "SharePoint/SharePoint-Designer-Error-Creating-Control-600x305.png");

            AddMappedImageUrl("9/o_SharePoint-Designer-Error-Creating-Control.png",
                "SharePoint/SharePoint-Designer-Error-Creating-Control-756x384.png");
        }

        private void AddMappedImageUrl(
            string oldImagePath,
            string newImagePath)
        {
            var key = string.Concat(_oldImageBasePath, "/", oldImagePath);

            var newImageUrl = new Uri(
                string.Concat(
                    "https://",
                    _newImageHost,
                    _newImageBasePath,
                    "/",
                    newImagePath));

            _imageUrlMap.Add(key, newImageUrl);
        }

        private string GetRelativeUrl(Uri url)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            if (url.IsAbsoluteUri == true && url.Host != _oldImageHost)
            {
                throw new ArgumentException(
                    $"URL ({url}) does not contain expected host ("
                        + _oldImageHost + ").",
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

        public Uri GetMappedUrl(Uri url)
        {
            var relativeUrl = GetRelativeUrl(url);

            return _imageUrlMap[relativeUrl];
        }

        public bool IsMappedUrl(Uri url)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }
            else if (url.IsAbsoluteUri == true && url.Host != _oldImageHost)
            {
                return false;
            }

            var relativeUrl = GetRelativeUrl(url);

            Uri mappedUrl = null;

            return _imageUrlMap.TryGetValue(relativeUrl, out mappedUrl);
        }
    }
}
