﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using BlogML.Xml;
using HtmlAgilityPack;
using Markdig;
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
                .Select(cat => new CategoryRef { Title = cat.Title, Id = cat.ID })
                .ToDictionary(x => x.Id);

            var config = new ReverseMarkdown.Config
            {
                GithubFlavored = true,
            };

            var mdConverter = new Converter(config);

            blog.Posts.ForEach(post =>
            {
                var slug = post.PostUrl.Substring(post.PostUrl.LastIndexOf('/') + 1);

                if (slug.EndsWith(".aspx"))
                {
                    slug = slug.Remove(slug.Length - ".aspx".Length);
                }

                var tags = GetTags(blogDoc, post.ID);

                var postHtml = post.Content.UncodedText;

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(postHtml);

                MassageTechnologyToolboxBlogPost(htmlDoc);

                if (tags.Count == 0)
                {
                    tags = GetTagsFromPostContent(htmlDoc);
                    RemoveTagsFromPostContent(htmlDoc);
                }

                postHtml = htmlDoc.DocumentNode.OuterHtml;

                var header = ComposeBlogHeader(post, categories, tags);
                var markdown = mdConverter.Convert(postHtml);

                markdown = Markdown.Normalize(markdown);

                markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(markdown);

                Console.WriteLine($"Writing {slug} ({post.Title})");

                // Organize blog posts by year/month/day
                var subfolder = $"{post.DateCreated:yyyy-MM-dd}"
                    .Replace("-", "\\");

                var postDir = Path.Combine(outDir, subfolder);

                if (!Directory.Exists(postDir))
                {
                    Directory.CreateDirectory(postDir);
                }

                WriteConvertedMarkdown(postDir, slug, header, markdown);
            });
        }

        static void WriteConvertedMarkdown(string outDir, string slug, string header, string markdown)
        {
            var outputFile = Path.Combine(outDir, slug + ".md");

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

                writer.WriteLine(markdown);
                writer.WriteLine();
            }
        }

        static string ComposeBlogHeader(BlogMLPost post, Dictionary<string, CategoryRef> categories, IEnumerable<string> tags)
        {
            var header = new StringBuilder("---");
            header.AppendLine();

            var escapedTitle = post.Title.Replace("\"", "\\\"");

            header.AppendLine($"title: \"{escapedTitle}\"");
            header.AppendLine($"date: {post.DateCreated:yyyy-MM-ddTHH:mm:ss}-07:00");

            if (post.DateModified != post.DateCreated)
            {
                header.AppendLine($"lastmod: {post.DateModified:yyyy-MM-ddTHH:mm:ss}-07:00");
            }

            if (post.HasExcerpt)
            {
                var escapedExcerpt = post.Excerpt.UncodedText
                    .Replace(@"\", @"\\")
                    .Replace("\"", "\\\"");

                header.AppendLine($"excerpt: \"{escapedExcerpt}\"");
            }

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

            var joinedTags = "\"" + string.Join("\", \"", tags) + "\"";

            header.Append(joinedTags);
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

        private static List<string> GetTagsFromPostContent(HtmlDocument doc)
        {
            // Parse tags from the content of a blog post that contains HTML
            // similar to the following:
            //
            //    <h3>
            //      Tags</h3>
            //    <ul>
            //      <li><a href="..." rel="tag">My System</a></li>
            //      <li><a href="..." rel="tag">Toolbox</a></li>
            //    </ul>
            //
            // For the example HTML above, a list containing "My System" and
            // "Toolbox" would be returned.
            
            var tagLinks = doc.DocumentNode.SelectNodes(
                "//h3[normalize-space() = 'Tags']/following-sibling::ul/li/a");

            var tags = new List<string>();

            if (tagLinks != null)
            {
                foreach (var tagLink in tagLinks)
                {
                    tags.Add(tagLink.InnerText);
                }
            }

            return tags;
        }

        private static void NormalizeWhitespaceInChildTextNodes(HtmlNode node)
        {
            // Replaces HTML content similar to the following:
            //
            //   <p>Some     <i>cool</i>                  <b>content</b>
            //     <a href="#">...</a>...
            //   </p>
            //
            // with:
            //
            //   <p>Some <i>cool</i> <b>content</b> <a href='#'>...</a>... </p>

            node.ChildNodes.ToList().ForEach((child) =>
            {
                if (child.Name == "#text")
                {
                    var normalizedText = child.InnerHtml
                        .Replace(Environment.NewLine, " ")
                        .Replace("\t", " ");

                    while (normalizedText.IndexOf("  ") != -1)
                    {
                        normalizedText = normalizedText.Replace("  ", " ");
                    }

                    Debug.Assert(normalizedText.IndexOf("  ") == -1);

                    child.InnerHtml = normalizedText;
                }
            });
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

        private static void RemoveTagsFromPostContent(HtmlDocument doc)
        {
            // Removes blog post content similar to the following:
            //
            //    <h3>
            //      Tags</h3>
            //    <ul>
            //      <li><a href="..." rel="tag">My System</a></li>
            //      <li><a href="..." rel="tag">Toolbox</a></li>
            //    </ul>

            var tagsList = doc.DocumentNode.SelectSingleNode(
                "//h3[normalize-space() = 'Tags']/following-sibling::ul");

            if (tagsList != null)
            {
                tagsList.Remove();
            }

            var tagsHeading = doc.DocumentNode.SelectSingleNode(
                "//h3[normalize-space() = 'Tags']");

            if (tagsHeading != null)
            {
                tagsHeading.Remove();
            }
        }

        private static void MassageTechnologyToolboxBlogPost(HtmlDocument doc)
        {
            FixSpacesInsideEmphasisElements(doc);
            MassageTechnologyToolboxBlogCallouts(doc);
            MassageTechnologyToolboxBlogConsoleBlocks(doc);
            MassageTechnologyToolboxBlogLinks(doc);
        }

        private static void MassageTechnologyToolboxBlogCallouts(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <blockquote class="note important">
            //       <div class="noteTitle">
            //         <strong>Important</strong></div>
            //       <div class="noteBody">
            //         Lorem ipsum
            //         dolor sit amet...
            //       </div>
            //   </blockquote>
            //
            // with:
            //
            //   <blockquote class="note important">
            //       <div class="noteTitle">
            //         <strong>Important</strong></div>
            //       <div class="noteBody">
            //         <p>Lorem ipsum dolor sit amet...</p></div>
            //   </blockquote>
            //
            // Note that without the <p> element in the note body, the Markdown
            // conversion results in extra indentation -- which causes the note
            // body to be interpreted as code.

            var elements = doc.DocumentNode.SelectNodes(
                "//blockquote[starts-with(@class, 'note')]/div[@class != 'noteTitle']");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var noteBody = element;

                    if (noteBody.FirstChild.Name != "p")
                    {
                        var newElement = doc.CreateElement("p");

                        noteBody.ChildNodes.ToList().ForEach(childNode =>
                        {
                            childNode.Remove();

                            newElement.AppendChild(childNode);
                        });

                        NormalizeWhitespaceInChildTextNodes(newElement);

                        noteBody.AppendChild(newElement);
                    }
                }
            }
        }

        private static void MassageTechnologyToolboxBlogConsoleBlocks(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <div class="consoleBlock">
            //     <kbd>cls</kbd><br/>
            //     <kbd>robocopy ...</kbd></div>
            //
            // with:
            //
            //   <div class="consoleBlock">
            //     <pre><code>cls</code>
            //     <code>robocopy ...</code></pre></div>

            var elements = doc.DocumentNode.SelectNodes("//div[@class = 'consoleBlock']/kbd");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    element.Name = "code";

                    var parent = element.ParentNode;

                    HtmlNode preElement = null;

                    if (element.ParentNode.LastChild.Name == "pre")
                    {
                        preElement = element.ParentNode.LastChild;

                        preElement.ChildNodes.Add(
                            doc.CreateTextNode(Environment.NewLine));
                    }

                    if (preElement == null)
                    {
                        preElement = doc.CreateElement("pre");
                        parent.ChildNodes.Add(preElement);
                    }

                    element.Remove();

                    preElement.ChildNodes.Add(element);
                }
            }
        }

        private static void MassageTechnologyToolboxBlogLinks(HtmlDocument doc)
        {
            var links = doc.DocumentNode.SelectNodes("//a");

            if (links != null)
            {
                foreach (var link in links)
                {
                    if (link.Attributes.Contains("href"))
                    {
                        var href = link.Attributes["href"].Value;
                        
                        if (href.StartsWith(
                            "/blog/jjameson/archive/",
                            StringComparison.OrdinalIgnoreCase) == true)
                        {
                            href = href.Replace(
                                "/blog/jjameson/archive/",
                                "/blog/jjameson/");

                            if (href.EndsWith(
                                ".aspx",
                                StringComparison.OrdinalIgnoreCase) == true)
                            {
                                href = href.Remove(href.Length - ".aspx".Length);
                            }

                            link.Attributes["href"].Value = href;
                        }
                    }
                }
            }
        }

        private static void FixSpacesInsideEmphasisElements(HtmlDocument doc)
        {
            // Replaces HTML content similar to the following:
            //
            //   ...click the <b>Advanced </b>button...
            //
            // with:
            //
            //   ...click the <b>Advanced</b> button...

            var nodes = doc.DocumentNode.SelectNodes(
                "//b[text() != normalize-space()]");

            if (nodes != null)
            {
                FixSpacesInsideEmphasisElements(nodes);
            }

            nodes = doc.DocumentNode.SelectNodes(
                "//i[text() != normalize-space()]");

            if (nodes != null)
            {
                FixSpacesInsideEmphasisElements(nodes);
            }

            nodes = doc.DocumentNode.SelectNodes(
                "//strong[text() != normalize-space()]");

            if (nodes != null)
            {
                FixSpacesInsideEmphasisElements(nodes);
            }
        }

        private static void FixSpacesInsideEmphasisElements(
            HtmlNodeCollection nodes)
        {
            // Replaces HTML content similar to the following:
            //
            //   ...click the<b> Advanced </b>button...
            //
            // with:
            //
            //   ...click the <b>Advanced</b> button...

            foreach (var node in nodes)
            {
                Debug.Assert(
                    node.Name == "b"
                    || node.Name == "i"
                    || node.Name == "strong");

                node.ChildNodes.ToList().ForEach((child) =>
                {
                    if (child.Name == "#text")
                    {
                        var trimmedText = child.InnerText.TrimStart();

                        if (trimmedText != child.InnerText)
                        {
                            var emphasisNode = child.ParentNode;
                            HtmlNode whitespaceTextNode = null;

                            if (emphasisNode.PreviousSibling != null
                                && emphasisNode.PreviousSibling.Name == "#text")
                            {
                                var textNode = emphasisNode.PreviousSibling;

                                if (textNode.InnerText.TrimEnd()
                                    != textNode.InnerText)
                                {
                                    whitespaceTextNode = textNode;
                                }
                            }

                            child.InnerHtml = trimmedText;

                            if (whitespaceTextNode == null)
                            {
                                whitespaceTextNode = node.OwnerDocument
                                    .CreateTextNode(" ");

                                node.ParentNode.InsertBefore(
                                    whitespaceTextNode,
                                    node);
                            }
                        }

                        trimmedText = child.InnerText.TrimEnd();

                        if (trimmedText != child.InnerText)
                        {
                            var emphasisNode = child.ParentNode;
                            HtmlNode whitespaceTextNode = null;

                            if (emphasisNode.NextSibling != null
                                && emphasisNode.NextSibling.Name == "#text")
                            {
                                var textNode = emphasisNode.NextSibling;

                                if (textNode.InnerText.TrimStart()
                                    != textNode.InnerText)
                                {
                                    whitespaceTextNode = textNode;
                                }
                            }

                            child.InnerHtml = trimmedText;

                            if (whitespaceTextNode == null)
                            {
                                whitespaceTextNode = node.OwnerDocument
                                    .CreateTextNode(" ");

                                node.ParentNode.InsertAfter(
                                    whitespaceTextNode,
                                    node);
                            }
                        }
                    }
                });
            }
        }
    }
}
