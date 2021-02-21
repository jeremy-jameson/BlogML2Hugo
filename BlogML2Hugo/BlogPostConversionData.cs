using BlogML.Xml;
using System;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class BlogPostConversionData
    {
        private readonly string _outputDirectory;
        private readonly BlogMLPost _post;

        public List<string> Aliases { get; private set; }

        public List<string> Categories { get; private set; }

        public string OutputDirectory { get { return _outputDirectory; } }

        public BlogMLPost Post { get { return _post; } }

        public string Slug { get; set; }

        public string Subfolder { get; set; }

        public List<string> Tags { get; private set; }

        public BlogPostConversionData(BlogMLPost post, string outputDirectory)
        {
            if (post == null)
            {
                throw new ArgumentNullException("post");
            }

            if (outputDirectory == null)
            {
                throw new ArgumentNullException("outputDirectory");
            }
            else if (string.IsNullOrWhiteSpace(outputDirectory) == true)
            {
                throw new ArgumentException(
                    "Output directory name must be specified.",
                    "outputDirectory");
            }

            _outputDirectory = outputDirectory;
            _post = post;

            Aliases = new List<string>();
            Categories = new List<string>();
            Tags = new List<string>();
        }
    }
}
