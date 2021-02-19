using BlogML.Xml;
using System;
using System.IO;
using System.Text;

namespace BlogML2Hugo
{
    public class SaveMarkdownStep : IPostConversionStep
    {
        public void Execute(PostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            Console.WriteLine($"Writing {postConversionData.Slug} ({post.Title})");

            var postDir = Path.Combine(postConversionData.OutputDirectory, postConversionData.Subfolder);

            if (!Directory.Exists(postDir))
            {
                Directory.CreateDirectory(postDir);
            }

            var header = ComposeBlogHeader(post, postConversionData);

            WriteConvertedMarkdown(postDir, postConversionData, header);
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

            var joinedCategories = "\"" + string.Join("\", \"", postConversionData.Categories) + "\"";

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
    }
}
