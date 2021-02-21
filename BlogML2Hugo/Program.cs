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
                var postConversionData = new BlogPostConversionData(post, outDir);

                IBlogPostConversionStep slugDeterminationStep =
                    new SlugDeterminationStep(blogUrlConverter);

                slugDeterminationStep.Execute(postConversionData);

                // Extract tags *before* preprocessing blog post (for example,
                // to allow the preprocessor to remove tags embedded in the
                // content of the post)

                IBlogPostConversionStep blogPostTagExtractor =
                    new TechnologyToolboxBlogPostTagExtractor(blogDoc);

                blogPostTagExtractor.Execute(postConversionData);

                IBlogPostConversionStep blogPostPreprocessor =
                    new TechnologyToolboxBlogPostPreprocessor(
                        imageUrlMapper, linkMapper);

                blogPostPreprocessor.Execute(postConversionData);

                IBlogPostConversionStep markdownConversionStep =
                    new MarkdownConversionStep();

                markdownConversionStep.Execute(postConversionData);

                IBlogPostConversionStep markdownNormalizationStep =
                    new MarkdownNormalizationStep();

                markdownNormalizationStep.Execute(postConversionData);

                IBlogPostConversionStep categoryLookupStep =
                    new CategoryLookupStep(categories);

                categoryLookupStep.Execute(postConversionData);

                Console.WriteLine($"Writing {postConversionData.Slug} ({post.Title})");

                var postDir = Path.Combine(outDir, postConversionData.Subfolder);

                IBlogPostConversionStep saveMarkdownStep =
                    new SaveMarkdownStep();

                saveMarkdownStep.Execute(postConversionData);

                convertedPostCount++;
            });

            Console.WriteLine($"Posts converted: {convertedPostCount}");
        }
    }
}
