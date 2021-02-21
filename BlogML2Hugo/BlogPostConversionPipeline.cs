using System;
using System.Collections.Generic;
using System.Linq;

namespace BlogML2Hugo
{
    public class BlogPostConversionPipeline
    {
        private readonly BlogPostConversionData _blogPostConversionData;

        private readonly LinkedList<IBlogPostConversionStep> _steps;

        public BlogPostConversionPipeline(
            BlogPostConversionData blogPostConversionData)
        {
            if (blogPostConversionData == null)
            {
                throw new ArgumentNullException("blogPostConversionData");
            }

            _blogPostConversionData = blogPostConversionData;

            _steps = new LinkedList<IBlogPostConversionStep>();
        }

        public void AddStep(IBlogPostConversionStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException("step");
            }

            _steps.AddLast(step);
        }

        public void Execute()
        {
            if (_steps.First == null)
            {
                throw new InvalidOperationException(
                    "Cannot execute an empty pipeline.");
            }

            _steps.ToList().ForEach(step =>
            {
                step.Execute(_blogPostConversionData);
            });
        }
    }
}
