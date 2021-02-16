using System;
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
        // List of "allowed" content in <kbd> elements (e.g. "Enter", "F5")
        private static HashSet<string> __allowedKbdContent =
            GetAllowedKbdContent();

        // List of content discoverd in <kbd> elements (used to generate the
        // "allowed" list by executing the migration and then inspecting the
        // list in the debugger before the process ends)
        private static HashSet<string> __discoveredKbdContent =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        private static void AppendHugoShortcodeParameterValue(
            StringBuilder sb,
            string parameterName,
            string parameterValue,
            bool omitIfNullOrWhitespace = true,
            bool appendNewLine = true)
        {
            if (omitIfNullOrWhitespace == true
                && string.IsNullOrWhiteSpace(parameterValue) == true)
            {
                return;
            }

            var encodedParameterValue =
                NormalizeWhitespace(parameterValue)
                .Replace("\"", "&quot;")
                .Replace("&quot;", "\\&quot;")
                .Replace("_", "%5F")
                .Trim();

            sb.Append($" {parameterName}=\"{encodedParameterValue}\"");

            if (appendNewLine == true)
            {
                sb.AppendLine();
            }
        }

        private static HashSet<string> GetAllowedKbdContent()
        {
            var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // This is just "throw away" code used for migrating blog content,
            // so just hard code the list of items 
            
            // The list was obtained by inspecting the "discovered" list in the
            // debugger after completing a test migration:
            //
            //   __discoveredKbdContent.OrderBy(x => x).ToList()

            list.Add("ALT+PRINT SCREEN");
            list.Add("CTRL+");
            list.Add("CTRL+ALT+DELETE");
            list.Add("CTRL+C");
            list.Add("CTRL+F");
            list.Add("CTRL+S");
            list.Add("Ctrl+Shift+B");
            list.Add("CTRL+SHIFT+P");
            list.Add("CTRL+V");
            list.Add("Delete");
            list.Add("Enter");
            list.Add("F5");
            list.Add("F7");
            list.Add("W");

            return list;
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
                    string normalizedText = NormalizeWhitespace(child.InnerHtml);

                    child.InnerHtml = normalizedText;
                }
            });
        }

        private static string NormalizeWhitespace(
            string html)
        {
            var normalizedText = html
                .Replace(Environment.NewLine, " ")
                .Replace("\t", " ");

            while (normalizedText.IndexOf("  ") != -1)
            {
                normalizedText = normalizedText.Replace("  ", " ");
            }

            Debug.Assert(normalizedText.IndexOf("  ") == -1);
            return normalizedText;
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
            ReplaceTechnologyToolboxBlogKbdElements(doc);
            MassageTechnologyToolboxBlogLinks(doc);
            MassageTechnologyToolboxBlogTables(doc);
            ReplaceTechnologyToolboxBlogImages(doc);
            ReplaceTechnologyToolboxBlogReferences(doc);
        }

        private static void MassageTechnologyToolboxBlogCallouts(HtmlDocument doc)
        {
            MassageTechnologyToolboxBlogDirectQuotes(doc);
            MassageTechnologyToolboxBlogNotes(doc);
        }

        private static void MassageTechnologyToolboxBlogDirectQuotes(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <blockquote class="directQuote errorMessage">
            //     Lorem ipsum dolor sit amet...
            //   </blockquote>
            //
            // with:
            //
            //   {{< blockquote "font-italic text-danger" >}}
            //
            //     Lorem ipsum dolor sit amet...
            //
            //   {{< /blockquote >}}
            //

            var elements = doc.DocumentNode.SelectNodes(
                "//blockquote[starts-with(@class, 'directQuote')]");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var blockquote = element;

                    NormalizeWhitespaceInChildTextNodes(blockquote);

                    var cssClass = "font-italic";

                    if (blockquote.Attributes["class"].Value
                        .Contains("errorMessage") == true)
                    {
                        cssClass += " text-danger";
                    }

                    string blockquoteShortcode =
                        "{{< blockquote \"" + cssClass + "\" >}}";

                    var shortcodeDiv = doc.CreateElement("div");

                    shortcodeDiv.AppendChild(
                        doc.CreateTextNode(blockquoteShortcode));

                    blockquote.ParentNode.InsertBefore(
                        shortcodeDiv,
                        blockquote);

                    blockquote.ParentNode.InsertBefore(
                        doc.CreateTextNode(Environment.NewLine),
                        blockquote);

                    shortcodeDiv = doc.CreateElement("div");

                    shortcodeDiv.AppendChild(
                        doc.CreateTextNode("{{< /blockquote >}}"));

                    blockquote.ParentNode.InsertAfter(
                        shortcodeDiv,
                        blockquote);

                    blockquote.ParentNode.InsertAfter(
                        doc.CreateTextNode(Environment.NewLine),
                        blockquote);

                    // Change <blockquote> to <div> so the content is converted
                    // to plain text in the Markdown
                    blockquote.Name = "div";
                }
            }
        }

        private static void MassageTechnologyToolboxBlogNotes(HtmlDocument doc)
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

                        // Add the <pre> element after the existing content
                        // (this way, if there are any additional <kbd>
                        // elements, they will be merged together into a
                        // single <pre> element -- while preserving the original
                        // order)
                        parent.ChildNodes.Add(preElement);
                    }

                    // Change the <kbd> element to <code> and move it inside
                    // the <pre> element
                    element.Name = "code";
                    element.Remove();
                    preElement.ChildNodes.Add(element);

                    // Since we changed the element from an inline element
                    // (<kbd>...</kbd>) to a block element
                    // (<pre><code>...</code></pre>), we must normalize
                    // any whitespace within the content (e.g. convert any
                    // line breaks to spaces). This ensures the content still
                    // renders as expected.
                    NormalizeWhitespaceInChildTextNodes(element);
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

        private static void MassageTechnologyToolboxBlogTables(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <table>
            //       <caption>My Table</caption>
            //       ...
            //   </table>
            //
            // with:
            //
            //   <p><strong>My Table</strong></p>
            //   <table>
            //       ...
            //   </table>
            //
            // This is used to avoid issues where <caption> elements are passed
            // through when converting from HTML to Markdown

            var elements = doc.DocumentNode.SelectNodes("//table");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var table = element;

                    var captionElement = table.SelectSingleNode(
                        "descendant::caption");

                    if (captionElement != null)
                    {
                        // Change the <caption> element to <p> and move it before
                        // the <table> element

                        captionElement.Name = "p";
                        captionElement.Remove();

                        table.ParentNode.InsertBefore(
                            captionElement,
                            table);

                        captionElement.InnerHtml = "<strong>" +
                            captionElement.InnerHtml.Trim() + "</strong>";
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

            FixSpacesInsideEmphasisElements(
                doc,
                "//b[text() != normalize-space()]");

            FixSpacesInsideEmphasisElements(
                doc,
                "//em[text() != normalize-space()]");

            FixSpacesInsideEmphasisElements(
                doc,
                "//i[text() != normalize-space()]");

            FixSpacesInsideEmphasisElements(
                doc,
                "//strong[text() != normalize-space()]");
        }

        private static void FixSpacesInsideEmphasisElements(
            HtmlDocument doc,
            string xpath)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);

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
                    || node.Name == "em"
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

        private static void RemoveIndentation(
            HtmlNode node)
        {
            if (node.PreviousSibling != null
                && node.PreviousSibling.Name == "#text")
            {
                var previousText = node.PreviousSibling.InnerText;

                if (string.IsNullOrWhiteSpace(previousText) == true)
                {
                    node.PreviousSibling.Remove();
                }
            }
        }

        private static void ReplaceTechnologyToolboxBlogImages(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //    <div class="image">
            //        <img height="600" width="537" src="..."
            //            class="screenshot"
            //            alt="Disk usage screenshot" />
            //        <div class="caption">
            //            Figure 2: Disk usage on Windows Vista after installing SP1, Office 2007, VS 2008,
            //            and Expression</div>
            //        <div class="imageLink">
            //            <a target="_blank" href="...">
            //                See full-sized image.</a>
            //        </div>
            //    </div>
            //
            // with:
            //
            //    <div class="image">
            //        <div>{{< figure
            //            src="..."
            //            alt="Disk usage screenshot"
            //            height="600"    width="537"
            //            title="Figure 2: Disk usage on Windows Vista after installing SP1, Office 2007, VS 2008, and Expression" >}}</div>
            //        
            //        <div class="imageLink">
            //            <a target="_blank" href="...">
            //                See full-sized image.</a>
            //        </div>
            //    </div>

            var elements = doc.DocumentNode.SelectNodes(
                "//div[@class = 'image']");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var imageDiv = element;

                    string captionText = null;

                    var captionDiv = imageDiv.SelectSingleNode(
                        "div[@class = 'caption']");

                    if (captionDiv != null)
                    {
                        captionText = captionDiv.InnerText;
                    }

                    imageDiv.Descendants("img").ToList().ForEach(img =>
                    {
                        var sb = new StringBuilder();
                        
                        var imgSrc = img.GetAttributeValue("src", null);
                        var imgAlt = img.GetAttributeValue("alt", null);
                        var imgHeight = img.GetAttributeValue("height", null);
                        var imgWidth = img.GetAttributeValue("width", null);
                        var imgTitle = img.GetAttributeValue("title", null);

                        if (string.IsNullOrWhiteSpace(captionText) == false)
                        {
                            imgTitle = captionText;
                        }

                        sb.AppendLine("{{< figure");

                        AppendHugoShortcodeParameterValue(sb, "src", imgSrc);
                        AppendHugoShortcodeParameterValue(sb, "alt", imgAlt);
                        AppendHugoShortcodeParameterValue(sb, "height", imgHeight,
                            appendNewLine: false);

                        AppendHugoShortcodeParameterValue(sb, "width", imgWidth);
                        AppendHugoShortcodeParameterValue(sb, "title", imgTitle,
                            appendNewLine: false);

                        sb.Append(" >}}");

                        var figureShortcode = sb.ToString();

                        var shortcodeDiv = img.OwnerDocument.CreateElement("div");

                        shortcodeDiv.AppendChild(
                            img.OwnerDocument.CreateTextNode(figureShortcode));

                        img.ParentNode.InsertBefore(shortcodeDiv, img);

                        img.Remove();

                        if (captionDiv != null
                            && captionDiv.ParentNode != null)
                        {
                            captionDiv.Remove();
                        }
                    });
                }
            }
        }

        private static void ReplaceTechnologyToolboxBlogKbdElements(
            HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //    <kbd>...</kbd>
            //
            // with:
            //
            //    {{< kbd "..." >}}
            //
            // when the inner text is specified in the list of allowed <kbd>
            // content. If the specified content is not allowed, the <kbd>
            // element is changed to a <code> element.
            //
            // For example:
            //
            //    <kbd>dir</kbd>
            //
            // is replaced with:
            //
            //    <code>dir</code>
            //
            // since "dir" is not a valid item on the keyboard.

            var elements = doc.DocumentNode.SelectNodes("//kbd");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var kbd = element;

                    var content = kbd.InnerText.Trim();

                    __discoveredKbdContent.Add(content);

                    if (__allowedKbdContent.Contains(content) == false)
                    {
                        kbd.Name = "code";
                        NormalizeWhitespaceInChildTextNodes(kbd);
                        continue;
                    }

                    var sb = new StringBuilder();

                    sb.Append("{{< kbd");

                    AppendHugoShortcodeParameterValue(sb, "key", content,
                        appendNewLine: false);

                    sb.Append(" >}}");

                    var kbdShortcode = sb.ToString();

                    var shortcodeSpan = doc.CreateElement("span");

                    shortcodeSpan.AppendChild(
                        kbd.OwnerDocument.CreateTextNode(kbdShortcode));

                    kbd.ParentNode.InsertBefore(shortcodeSpan, kbd);

                    kbd.Remove();
                }
            }
        }

        private static void ReplaceTechnologyToolboxBlogReferences(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <div class="reference">
            //     <cite>"Access is denied" error message...</cite> 
            //     <div class="referenceLink">
            //       <a href="http://support.microsoft.com/kb/867466">
            //         http://support.microsoft.com/kb/867466</a>
            //     </div>
            //   </div>
            //
            // with:
            //
            //    <div class="reference">
            //        <div>{{< reference
            //            title=""Access is denied" error message..."
            //            linkHref="http://support.microsoft.com/kb/867466" >}}</div>
            //    </div>
            //
            // This is used to avoid issues where <cite> elements are passed
            // through when converting from HTML to Markdown

            var elements = doc.DocumentNode.SelectNodes(
                "//div[@class = 'reference']");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var referenceDiv = element;

                    var citeElement = referenceDiv.SelectSingleNode(
                        "descendant::cite");

                    if (citeElement == null)
                    {
                        // If the "reference" block does not contain a <cite>
                        // element then there is no issue using the default
                        // HTML to Markdown conversion process (i.e. skip this
                        // "reference" block)
                        continue;
                    }

                    string title = citeElement.InnerText;
                    string linkHref = null;
                    string linkText = null;

                    var referenceLink = referenceDiv.SelectSingleNode(
                        "descendant::div[@class = 'referenceLink']/a");

                    linkText = referenceLink.InnerText.Trim();
                    linkHref = referenceLink.GetAttributeValue("href", null);

                    var sb = new StringBuilder();

                    sb.Append("{{< reference");

                    AppendHugoShortcodeParameterValue(sb, "title", title,
                        appendNewLine: false);

                    AppendHugoShortcodeParameterValue(sb, "linkHref", linkHref,
                        appendNewLine: false);

                    if (linkText != linkHref)
                    {
                        AppendHugoShortcodeParameterValue(
                            sb, "linkText", linkText, appendNewLine: false);
                    }

                    sb.Append(" >}}");

                    var referenceShortcode = sb.ToString();

                    // Remove indentation on "reference" <div> elements to
                    // avoid issues where content on a number of blog posts
                    // is indented when converting to Markdown (and therefore
                    // incorrectly formatted as code)
                    RemoveIndentation(referenceDiv);
                    RemoveIndentation(citeElement);
                    RemoveIndentation(referenceLink);

                    // Insert the "reference" shortcode at the location where
                    // the <cite> element was found
                    citeElement.ParentNode.InsertBefore(
                        doc.CreateTextNode(referenceShortcode),
                        citeElement);

                    // Remove the <cite> and <div class="referenceLink">
                    // elements (since these have been replaced by the new
                    // "{{< reference ... >}}" shortcode
                    citeElement.Remove();
                    referenceLink.Remove();
                }
            }
        }
    }
}
