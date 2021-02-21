using BlogML;
using BlogML.Xml;
using HtmlAgilityPack;
using System;

namespace BlogML2Hugo
{
    public class ImageUrlReplacementStep : IBlogPostConversionStep
    {
        private readonly IUrlMapper _imageUrlMapper;

        public ImageUrlReplacementStep(
            IUrlMapper imageUrlMapper)
        {
            if (imageUrlMapper == null)
            {
                throw new ArgumentNullException("imageUrlMapper");
            }

            _imageUrlMapper = imageUrlMapper;
        }

        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            var postHtml = post.Content.UncodedText;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(postHtml);

            ReplaceBlogImageUrls(htmlDoc);

            post.Content = BlogMLContent.Create(
                htmlDoc.DocumentNode.OuterHtml,
                ContentTypes.Html);
        }

        private void ReplaceBlogImageUrls(HtmlDocument doc)
        {
            var elements = doc.DocumentNode.SelectNodes("//img");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var imageSrc = element.GetAttributeValue("src", null);

                    if (string.IsNullOrWhiteSpace(imageSrc) == false)
                    {
                        var originalUrl = new Uri(
                            imageSrc,
                            UriKind.RelativeOrAbsolute);

                        if (_imageUrlMapper.IsMappedUrl(originalUrl) == true)
                        {
                            var mappedUrl = _imageUrlMapper.GetMappedUrl(originalUrl);

                            element.Attributes["src"].Value = mappedUrl.AbsoluteUri;
                        }
                    }
                }
            }

            elements = doc.DocumentNode.SelectNodes("//a");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var href = element.GetAttributeValue("href", null);

                    if (string.IsNullOrWhiteSpace(href) == false)
                    {
                        var originalUrl = new Uri(href, UriKind.RelativeOrAbsolute);

                        if (_imageUrlMapper.IsMappedUrl(originalUrl) == true)
                        {
                            var mappedUrl = _imageUrlMapper.GetMappedUrl(originalUrl);

                            element.Attributes["href"].Value = mappedUrl.AbsoluteUri;
                        }
                    }
                }
            }
        }
    }
}
