using BlogML.Xml;
using System;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class BlogPostConversionPipelineBuilder
    {
        private string _outputDirectory;
        private BlogMLPost _post;

        private readonly List<IBlogPostConversionStep> _steps =
            new List<IBlogPostConversionStep>();

        public BlogPostConversionPipelineBuilder AddStep(
            IBlogPostConversionStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException("step");
            }

            _steps.Add(step);

            return this;
        }

        public BlogPostConversionPipeline Build()
        {
            var blogPostConversionData = new BlogPostConversionData(_post, _outputDirectory);

            var pipeline = new BlogPostConversionPipeline(
                blogPostConversionData);

            _steps.ForEach(step => { pipeline.AddStep(step); });

            return pipeline;
        }

        public BlogPostConversionPipelineBuilder ForPost(BlogMLPost post)
        {
            if (post == null)
            {
                throw new ArgumentNullException("post");
            }

            _post = post;

            return this;
        }

        public BlogPostConversionPipelineBuilder WithOutputDirectory(
            string outputDirectory)
        {
            if (outputDirectory == null)
            {
                throw new ArgumentNullException("outputDirectory");
            }
            else if (string.IsNullOrWhiteSpace(outputDirectory) == true)
            {
                throw new ArgumentException(
                    "Output directory must be specified.",
                    "outputDirectory");
            }

            _outputDirectory = outputDirectory;

            return this;
        }
    }
}
