using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using BlogML.Xml;
using ReverseMarkdown;

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
                .Select(cat => new CategoryRef {Title = cat.Title, Id = cat.ID})
                .ToDictionary(x => x.Id);

            var mdConverter = new Converter();

            blog.Posts.ForEach(post =>
            {
                var slug = post.PostUrl.Substring(post.PostUrl.LastIndexOf('/') + 1);
                var tags = GetTags(blogDoc, post.ID);

                var header = ComposeBlogHeader(post, categories, tags);
                var markdown = mdConverter.Convert(post.Content.UncodedText);

                Console.WriteLine($"Writing {slug} ({post.Title})");

                WriteConvertedMarkdown(outDir, slug, header, markdown);
            });
        }

        static void WriteConvertedMarkdown(string outDir, string slug, string header, string markdown)
        {
            var outputFile = Path.Combine(outDir, slug + ".md");
            
            using (var file = File.OpenWrite(outputFile))
            using (var writer = new StreamWriter(file))
            {
                writer.WriteLine();
                writer.Write(header);
                writer.Flush();

                writer.WriteLine();
                writer.WriteLine();

                writer.WriteLine(markdown);
                writer.WriteLine();
            }
        }

        static string ComposeBlogHeader(BlogMLPost post, Dictionary<string, CategoryRef> categories, IEnumerable<string> tags)
        {
            var header = new StringBuilder("---");
            header.AppendLine();
            header.AppendLine($"title: \"{post.Title}\"");
            header.AppendLine($"date: {post.DateCreated:yyyy-MM-ddTHH:mm:ss}+08:00");
            header.AppendLine($"lastmod: {post.DateModified:yyyy-MM-ddTHH:mm:ss}+08:00");
            header.AppendLine($"draft: false");
            header.Append($"categories: [");
            foreach (BlogMLCategoryReference category in post.Categories)
            {
                var cat = categories[category.Ref];
                header.Append($"\"{cat.Title}\", ");
            }
            header.AppendLine("]");

            header.Append($"tags: [");
            foreach (string tag in tags)
            {
                header.Append($"\"{tag}\", ");
            }
            header.AppendLine("]");
            // header.AppendLine("isCJKLanguage: true");

            
            header.AppendLine("---");
            return header.ToString();
        }

        static List<string> GetTags(XmlDocument blogMLDoc, string postId)
        {

            var tags = new List<string>();
            var root = blogMLDoc.DocumentElement; 
            var mgr = new XmlNamespaceManager(blogMLDoc.NameTable);
            mgr.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
            mgr.AddNamespace("blogml", "http://www.blogml.com/2006/09/BlogML");
            

            var tagList = root.SelectNodes($"//blogml:post[@id='{postId}']/blogml:tags/blogml:tag", mgr);
            
            foreach (XmlNode tag in tagList)
            {
                tags.Add(tag.Attributes["ref"].InnerText);
            }

            return tags;
        }
        
        
    }
}
