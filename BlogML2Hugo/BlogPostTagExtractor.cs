using BlogML.Xml;
using System;
using System.Collections.Generic;
using System.Xml;

namespace BlogML2Hugo
{
    class BlogPostTagExtractor : IBlogPostTagExtractor
    {
        private readonly XmlDocument _blogMLDoc;

        public BlogPostTagExtractor(XmlDocument blogMLDoc)
        {
            if (blogMLDoc == null)
            {
                throw new ArgumentNullException("blogMLDoc");
            }

            _blogMLDoc = blogMLDoc;
        }

        public virtual IEnumerable<string> GetTags(BlogMLPost post)
        {
            if (post == null)
            {
                throw new ArgumentNullException("post");
            }

            var tags = new List<string>();
            var root = _blogMLDoc.DocumentElement;
            var mgr = new XmlNamespaceManager(_blogMLDoc.NameTable);
            mgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
            mgr.AddNamespace("blogml", "http://www.blogml.com/2006/09/BlogML");


            var tagList = root.SelectNodes($"//blogml:post[@id='{post.ID}']/blogml:tags/blogml:tag", mgr);

            foreach (XmlNode tag in tagList)
            {
                tags.Add(tag.Attributes["ref"].InnerText);
            }

            return tags;
        }
    }
}
