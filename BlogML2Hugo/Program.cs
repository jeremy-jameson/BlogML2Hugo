using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using BlogML.Xml;
using Html2Markdown;
using Html2Markdown.Replacement;
using Html2Markdown.Scheme;

namespace BlogML2Hugo
{
    class Program
    {
        static void Main(string[] args)
        {
            var inputXml = "blogml.xml";
            var outDir = "/content/post/of/hugo/";
                
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
            
            
            var categories = blog.Categories.Select(cat => new CategoryRef{Title = cat.Title, Id = cat.ID}).ToDictionary(x => x.Id);
//            var mdConverter = new Converter(new DivPScheme());
            var mdConverter = new Converter();
            
            blog.Posts.ForEach(post =>
            {
                var slug = post.PostUrl.Substring(post.PostUrl.LastIndexOf('/') + 1);
                var tags = GetTags(blogDoc, post.ID);

                var header = ComposeBlogHeader(post, categories, tags);
                var markdown = mdConverter.Convert(post.Content.UncodedText);

                Console.WriteLine($"Writing {slug} ({post.Title})");
                
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
            });
            
        }

        private static string ComposeBlogHeader(BlogMLPost post, Dictionary<string, CategoryRef> categories, IEnumerable<string> tags)
        {
            var header = new StringBuilder("---");
            header.AppendLine();
            header.AppendLine($"title: \"{post.Title}\"");
            header.AppendLine($"date: {post.DateCreated:yyyy-MM-ddTHH:mm:ss}+08:00");
            header.AppendLine($"lastmod: {post.DateModified:yyyy-MM-ddTHH:mm:ss}+08:00");
            // header.AppendLine($"description: \"{post.}\"");   post.Excerpt
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
            header.AppendLine("isCJKLanguage: true");

            if (post.HasExcerpt)
            {
                header.AppendLine("]");    
            }
            
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

    internal class DivPScheme : IScheme
    {
        public IList<IReplacer> Replacers()
        {
            var replacers = new List<IReplacer>(new Markdown().Replacers());
            replacers.Add(new RegexReplacer
            {
                Pattern = @"<(div|p)[\s""'=a-zA-Z\-_0-9%#]*?>",
                Replacement = string.Empty
            });
            
            replacers.Add(new RegexReplacer
            {
                Pattern = @"</(div|p)\s*?>",
                Replacement = Environment.NewLine
            });
            
//            replacers.Add(new RegexMatchReplacer
//            {
//                Pattern = @"/(image|file)\.axd\?(picture|file)\=[a-zA-Z0-9%.\-_\s]+(\s""image"")?\)",
//                Match = "%2[fF]",
//                Replacement = "/"
//            });
//            replacers.Add(new RegexMatchReplacer
//            {
//                Pattern = @"/(image|file)\.axd\?(picture|file)\=[a-zA-Z0-9%/.\-_\s]+(\s""image"")?\)",
//                Match = @"/(image|file)\.axd\?(picture|file)\=",
//                Replacement = "/files/"
//            });

            return replacers;
        }
    }
    
    internal class RegexReplacer : IReplacer
    {
        public string Pattern { get; set; }

        public string Replacement { get; set; }

        public string Replace(string html)
        {
            return new Regex(this.Pattern).Replace(html, this.Replacement);
        }
    }
    
    internal class RegexMatchReplacer : IReplacer
    {
        public string Pattern { get; set; }
        
        public string Match { get; set; }

        public string Replacement { get; set; }

        public string Replace(string html)
        {
            var regex = new Regex(this.Pattern);
            var matchRegex = new Regex(this.Match);
            
            var matches = regex.Matches(html);

            var resultBuilder = new StringBuilder();
            var lastIndex = 0;

            var allMatches = matches.Where(match => match.Success).OrderBy(match => match.Index).ToList();
            foreach (var match in allMatches)
            {
                resultBuilder.Append(html.Substring(lastIndex, match.Index - lastIndex));
                lastIndex = lastIndex + match.Length;
                    
                var replaced = matchRegex.Replace(match.Value, this.Replacement);
                resultBuilder.Append(replaced);
            }
            
            resultBuilder.Append(html.Substring(lastIndex, html.Length - lastIndex));
            return resultBuilder.ToString();
        }
    }

    internal class CategoryRef
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }
}
