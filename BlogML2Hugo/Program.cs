using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using BlogML.Xml;
using Markdig;

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

            var linkMapper = new LinkMapper(blogUrlConverter);

            blog.Posts.ForEach(post =>
            {
                var postConversionData = new PostConversionData(post);

                // Extract tags *before* preprocessing blog post (for example,
                // to allow the preprocessor to remove tags embedded in the
                // content of the post)

                IPostConversionStep blogPostTagExtractor =
                    new TechnologyToolboxBlogPostTagExtractor(blogDoc);

                blogPostTagExtractor.Execute(postConversionData);

                IPostConversionStep blogPostPreprocessor =
                    new TechnologyToolboxBlogPostPreprocessor(linkMapper);

                blogPostPreprocessor.Execute(postConversionData);

                IPostConversionStep markdownConversionStep =
                    new MarkdownConversionStep();

                markdownConversionStep.Execute(postConversionData);

                var markdown = Markdown.Normalize(postConversionData.Markdown);

                markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(
                    markdown);

                markdown = ReverseMarkdownHelper.DecodeAfterConversion(
                    markdown);

                postConversionData.Markdown = markdown;

                Console.WriteLine($"Writing {postConversionData.Slug} ({post.Title})");

                var postDir = Path.Combine(outDir, postConversionData.Subfolder);

                if (!Directory.Exists(postDir))
                {
                    Directory.CreateDirectory(postDir);
                }

                var header = ComposeBlogHeader(post, categories, postConversionData);

                WriteConvertedMarkdown(postDir, postConversionData, header);

                convertedPostCount++;
            });

            Console.WriteLine($"Posts converted: {convertedPostCount}");
        }

        static void WriteConvertedMarkdown(
            string outDir,
            PostConversionData postConversionData,
            string header)
        {
            var outputFile = Path.Combine(outDir, postConversionData.Slug + ".md");

            if (File.Exists(outputFile) == true)
            {
                File.Delete(outputFile);
            }

            using (var file = File.OpenWrite(outputFile))
            using (var writer = new StreamWriter(file))
            {
                writer.Write(header);
                writer.Flush();

                writer.WriteLine();

                writer.WriteLine(postConversionData.Markdown);
                writer.WriteLine();
            }
        }

        static string ComposeBlogHeader(
            BlogMLPost post,
            Dictionary<string, CategoryRef> categories,
            PostConversionData postConversionData)
        {
            var header = new StringBuilder("---");
            header.AppendLine();

            var escapedTitle = post.Title.Replace("\"", "\\\"");

            header.AppendLine($"title: \"{escapedTitle}\"");
            header.AppendLine($"date: {post.DateCreated.ToLocalTime():yyyy-MM-ddTHH:mm:ssK}");

            if (post.DateModified != post.DateCreated)
            {
                header.AppendLine($"lastmod: {post.DateModified.ToLocalTime():yyyy-MM-ddTHH:mm:ssK}");
            }

            if (post.HasExcerpt)
            {
                var escapedExcerpt = post.Excerpt.UncodedText
                    .Replace(@"\", @"\\")
                    .Replace("\"", "\\\"");

                header.AppendLine($"excerpt: \"{escapedExcerpt}\"");
            }

            var joinedAliases = "\"" + string.Join("\", \"", postConversionData.Aliases) + "\"";

            header.Append($"aliases: [");
            header.Append(joinedAliases);
            header.AppendLine("]");

            // TODO: Remove "draft" from front matter for final conversion
            header.AppendLine($"draft: true");

            var categoryList = new List<string>();

            foreach (BlogMLCategoryReference category in post.Categories)
            {
                var cat = categories[category.Ref];
                categoryList.Add(cat.Title);
            }

            var joinedCategories = "\"" + string.Join("\", \"", categoryList) + "\"";

            header.Append($"categories: [");
            header.Append(joinedCategories);
            header.AppendLine("]");

            header.Append($"tags: [");

            var joinedTags = "\"" + string.Join("\", \"", postConversionData.Tags) + "\"";

            header.Append(joinedTags);
            header.AppendLine("]");
            // header.AppendLine("isCJKLanguage: true");


            header.AppendLine("---");
            return header.ToString();
        }


        private static string RemoveTrailingSpacesFromEmptyBlockquoteLines(
            string markdown)
        {
            int index = markdown.IndexOf("\n" + "> " + "\n");

            if (index == -1)
            {
                return markdown;
            }

            return markdown.Replace("\n> \n", "\n>\n");
        }
    }
}
