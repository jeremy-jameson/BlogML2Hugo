using BlogML.Xml;
using System;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class PostConversionData
    {
        private readonly BlogMLPost _post;

        public List<string> Aliases { get; private set; }

        public string Markdown { get; set; }

        public BlogMLPost Post { get { return _post; } }

        public string Slug { get; set; }

        public string Subfolder { get; set; }

        public List<string> Tags { get; private set; }

        public PostConversionData(BlogMLPost post)
        {
            if (post == null)
            {
                throw new ArgumentNullException("post");
            }

            _post = post;

            Aliases = new List<string>();
            Tags = new List<string>();
        }
    }
}
