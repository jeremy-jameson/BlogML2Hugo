using System;
using System.IO;
using System.Linq;
using System.Xml;
using BlogML.Xml;

namespace BlogML2Hugo
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintError("Please specify BlogML xml file and output directory for coverted markdown files.");
                Environment.Exit(1);
            }

            var inputXml = args[0];
            var outDir = args[1];
            if (!File.Exists(inputXml))
            {
                PrintError($"File '{inputXml}' does not exisit.");
                Environment.Exit(1);
            }

            try
            {
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                ConvertBlog(inputXml, outDir);
            }
            catch (Exception ex)
            {
                PrintError($"Error processing '{inputXml}':");
                PrintError(ex.Message);
                PrintError(ex.StackTrace);
                Environment.Exit(2);
            }
        }

        static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Error.WriteLine(message);

            Console.ResetColor();
        }

        static void ConvertBlog(string inputXml, string outDir)
        {
            BlogMLBlog blog;
            using (var fs = File.OpenRead(inputXml))
            {
                blog = BlogMLSerializer.Deserialize(fs);
            }

            // Load the document and set the root element.  
            var blogDoc = new XmlDocument();
            using (XmlTextReader tr = new XmlTextReader(inputXml))
            {
                tr.Namespaces = false;
                blogDoc.Load(inputXml);
            }


            var categories = blog.Categories
                .Select(cat => new CategoryRef { Title = cat.Title, Id = cat.ID })
                .ToDictionary(x => x.Id);

            var convertedPostCount = 0;

            IBlogUrlConverter blogUrlConverter =
                new TechnologyToolboxBlogUrlConverter();

            var imageUrlMapper = new TechnologyToolboxImageUrlMapper();

            var linkMapper = new LinkMapper(blogUrlConverter);

            blog.Posts.ForEach(post =>
            {
                var pipeline = new BlogPostConversionPipelineBuilder()
                    .ForPost(post)
                    .WithOutputDirectory(outDir)
                    .AddStep(new SlugDeterminationStep(blogUrlConverter))
                    // Extract tags *before* preprocessing blog post (for
                    // example, to allow the preprocessing step to remove tags
                    // embedded in the content of the post)
                    .AddStep(new TechnologyToolboxTagExtractionStep(blogDoc))
                    // Replace image URLs *before* preprocessing blog post (for
                    // example, to allow the preprocessing step to replace <img>
                    // elements embedded in the content of the post with Hugo
                    // "figure" shortcodes)
                    .AddStep(new ImageUrlReplacementStep(imageUrlMapper))
                    .AddStep(new FixHtmlFormattingIssuesStep())
                    .AddStep(new TechnologyToolboxBlogPostPreprocessor(
                        linkMapper))
                    .AddStep(new MarkdownConversionStep())
                    .AddStep(new MarkdownNormalizationStep())
                    .AddStep(new CategoryLookupStep(categories))
                    .AddStep(new SaveMarkdownStep())
                    .Build();

                pipeline.Execute();

                convertedPostCount++;
            });

            Console.WriteLine($"Posts converted: {convertedPostCount}");
        }
    }
}
