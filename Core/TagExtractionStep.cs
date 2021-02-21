using System;
using System.Xml;

namespace BlogML2Hugo.Core
{
    public class TagExtractionStep : IBlogPostConversionStep
    {
        private readonly XmlDocument _blogMLDoc;

        public TagExtractionStep(XmlDocument blogMLDoc)
        {
            if (blogMLDoc == null)
            {
                throw new ArgumentNullException("blogMLDoc");
            }

            _blogMLDoc = blogMLDoc;
        }

        public virtual void Execute(BlogPostConversionData postConversionData)
        {
            if (postConversionData == null)
            {
                throw new ArgumentNullException("postConversionData");
            }

            var post = postConversionData.Post;

            var root = _blogMLDoc.DocumentElement;
            var mgr = new XmlNamespaceManager(_blogMLDoc.NameTable);
            mgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
            mgr.AddNamespace("blogml", "http://www.blogml.com/2006/09/BlogML");


            var tagList = root.SelectNodes($"//blogml:post[@id='{post.ID}']/blogml:tags/blogml:tag", mgr);

            foreach (XmlNode tag in tagList)
            {
                postConversionData.Tags.Add(tag.Attributes["ref"].InnerText);
            }
        }
    }
}
