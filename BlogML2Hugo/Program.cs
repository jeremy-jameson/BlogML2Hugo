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

            var convertedPostCount = 0;

            IBlogUrlConverter blogUrlConverter =
                new TechnologyToolboxBlogUrlConverter();

            var linkMapper = new LinkMapper(blogUrlConverter);

            blog.Posts.ForEach(post =>
            {
                FixTechnologyToolboxBlogPostDates(post);

                var postUrl = new Uri(post.PostUrl);

                var slug = blogUrlConverter.GetSlug(postUrl);

                // Organize blog posts by year/month/day

                var subfolder = $"{post.DateCreated.ToLocalTime():yyyy-MM-dd}"
                    .Replace("-", "\\");

                // Since the dates for blog posts have been updated -- for
                // example, to account for the migration from MSDN (Telligent)
                // to Technology Toolbox (Subtext) -- overwrite the post URLs
                // as necessary. In other words, Subtext didn't have the correct
                // dates for the blog posts migrated from MSDN, so the URLs
                // specified in the BlogML file are often "off" by a day.

                Uri url = new Uri(
                    string.Concat(
                        "https://www.technologytoolbox.com/blog/jjameson/archive/",
                        subfolder.Replace("\\", "/"),
                        "/",
                        slug,
                        ".aspx"));

                var postAliases = new List<string>();

                postAliases.Add(postUrl.PathAndQuery);

                if (post.PostUrl != url.AbsoluteUri)
                {
                    post.PostUrl = url.AbsoluteUri;

                    postAliases.Add(url.PathAndQuery);
                }

                linkMapper.Add(url);

                IBlogPostTagExtractor blogPostTagExtractor =
                    new TechnologyToolboxBlogPostTagExtractor(blogDoc);

                var tags = blogPostTagExtractor.GetTags(post);
                var postHtml = post.Content.UncodedText;

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(postHtml);

                ProcessTechnologyToolboxBlogPost(htmlDoc, linkMapper);

                postHtml = htmlDoc.DocumentNode.OuterHtml;

                var markdown = mdConverter.Convert(postHtml);

                markdown = Markdown.Normalize(markdown);

                markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(
                    markdown);

                markdown = ReverseMarkdownHelper.DecodeAfterConversion(
                    markdown);

                Console.WriteLine($"Writing {slug} ({post.Title})");

                var postDir = Path.Combine(outDir, subfolder);

                if (!Directory.Exists(postDir))
                {
                    Directory.CreateDirectory(postDir);
                }

                var header = ComposeBlogHeader(post, categories, tags, postAliases);

                WriteConvertedMarkdown(postDir, slug, header, markdown);

                convertedPostCount++;
            });

            Console.WriteLine($"Posts converted: {convertedPostCount}");
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

        static string ComposeBlogHeader(
            BlogMLPost post,
            Dictionary<string, CategoryRef> categories,
            IEnumerable<string> tags,
            List<string> postAliases)
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

            var joinedAliases = "\"" + string.Join("\", \"", postAliases) + "\"";

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

            var joinedTags = "\"" + string.Join("\", \"", tags) + "\"";

            header.Append(joinedTags);
            header.AppendLine("]");
            // header.AppendLine("isCJKLanguage: true");


            header.AppendLine("---");
            return header.ToString();
        }

        private static void FixTechnologyToolboxBlogPostDates(BlogMLPost post)
        {
            // When migrating blog posts from MSDN (running Telligent) to
            // Technoogy Toolbox (running Subtext), the dates were offset
            // by a number of hours. Consequently the "day" portion of URLs
            // did not always match "date created" -- i.e. it could be off
            // by a day. This was likely due to the "lossy" date format
            // specified in the BlogML file used to export content from
            // Telligent into Subtext.
            //
            // In other words, all of the issues due to date/URL mismatch
            // seems to be due to dateCreated.ToString("s") -- since that
            // format does not preserve the timezone information. (Note
            // that Subtext suffers a similar issue in the BlogML export.)
            //
            // To resolve these issues (hopefully for the last time),
            // convert "Unspecified" DateTime values into "Local" DateTime
            // equivalents. However, note that blog posts created in
            // Subtext (i.e. not migrated from MSDN/Telligent) already
            // have the correct timestamp values and therefore should simply
            // be "forced" from "Unspecified" to "Local" without adjusting
            // the DateTime values.

            var msdnBlogCutoverDate = new DateTime(2011, 9, 3);

            if (post.DateCreated.Kind == DateTimeKind.Unspecified)
            {
                var offset = TimeZoneInfo.Local.GetUtcOffset(post.DateCreated);

                if (post.DateCreated < msdnBlogCutoverDate)
                {
                    post.DateCreated = post.DateCreated.Add(-offset);
                }

                post.DateCreated = DateTime.SpecifyKind(
                    post.DateCreated,
                    DateTimeKind.Local);
            }

            if (post.DateModified.Kind == DateTimeKind.Unspecified)
            {
                var offset = TimeZoneInfo.Local.GetUtcOffset(post.DateCreated);

                if (post.DateModified < msdnBlogCutoverDate)
                {
                    post.DateModified = post.DateModified.Add(-offset);
                }

                post.DateModified = DateTime.SpecifyKind(
                    post.DateModified,
                    DateTimeKind.Local);
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

        private static void ProcessTechnologyToolboxBlogPost(
            HtmlDocument doc,
            LinkMapper linkMapper)
        {
            FixSpacesInsideEmphasisElements(doc);
            ProcessTechnologyToolboxBlogCallouts(doc);

            ProcessTechnologyToolboxBlogKbdContentInsideConsoleBlocks(doc);

            ProcessTechnologyToolboxBlogConsoleBlocks(doc);
            ReplaceTechnologyToolboxBlogKbdElements(doc);
            ProcessTechnologyToolboxBlogLinks(doc, linkMapper);
            ProcessTechnologyToolboxBlogTableCells(doc);
            ProcessTechnologyToolboxBlogTables(doc);
            ReplaceTechnologyToolboxBlogImages(doc);
            ReplaceTechnologyToolboxBlogReferences(doc);
            ReplaceTechnologyToolboxBlogSampElements(doc);
        }

        private static void ProcessTechnologyToolboxBlogCallouts(HtmlDocument doc)
        {
            ProcessTechnologyToolboxBlogDirectQuotes(doc);
            ProcessTechnologyToolboxBlogNotes(doc);
        }

        private static void ProcessTechnologyToolboxBlogDirectQuotes(HtmlDocument doc)
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

                    HtmlDocumentHelper.NormalizeWhitespaceInChildTextNodes(
                        blockquote);

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

        private static void ProcessTechnologyToolboxBlogNotes(HtmlDocument doc)
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

                        HtmlDocumentHelper.NormalizeWhitespaceInChildTextNodes(
                            newElement);

                        noteBody.AppendChild(newElement);
                    }
                }
            }
        }

        private static void ProcessTechnologyToolboxBlogConsoleBlocks(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <div class="consoleBlock">
            //     ...
            //   </div>
            //
            // with:
            //
            //   {{< console-block-start >}}
            //
            //   <div class="consoleBlock">
            //     ...
            //   </div>
            //
            //   {{< console-block-end >}}

            var elements = doc.DocumentNode.SelectNodes("//div[@class = 'consoleBlock']");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    bool hasParagraphContent = element.Descendants("p").Any();
                    bool hasPreformattedContent = element.Descendants("pre").Any();

                    if (hasParagraphContent == false
                        && hasPreformattedContent == true)
                    {
                        continue;
                    }

                    // Insert paragraph containing Hugo shortcode
                    // "{{< console-block-start >}}" before the
                    // <div class="consoleBlock"> element

                    var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    var shortcodeNode = shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("div")
                        .WithName("console-block-start")
                        .Build();

                    var paragraph = element.OwnerDocument.CreateElement("p");

                    paragraph.ChildNodes.Add(shortcodeNode);

                    element.ParentNode.InsertBefore(paragraph, element);

                    // Insert paragraph containing Hugo shortcode
                    // "{{< console-block-end >}}" after the
                    // <div class="consoleBlock"> element

                    shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    shortcodeNode = shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("div")
                        .WithName("console-block-end")
                        .Build();

                    paragraph = element.OwnerDocument.CreateElement("p");

                    paragraph.ChildNodes.Add(shortcodeNode);

                    element.ParentNode.InsertAfter(paragraph, element);
                }
            }
        }

        private static void ProcessTechnologyToolboxBlogKbdContentInsideConsoleBlocks(HtmlDocument doc)
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
                    HtmlDocumentHelper.NormalizeWhitespaceInChildTextNodes(
                        element);
                }
            }
        }

        private static void ProcessTechnologyToolboxBlogLinks(
            HtmlDocument doc,
            LinkMapper linkMapper)
        {
            var links = doc.DocumentNode.SelectNodes("//a");

            if (links != null)
            {
                foreach (var link in links)
                {
                    if (link.Attributes.Contains("href"))
                    {
                        var href = link.Attributes["href"].Value;

                        var url = new Uri(href, UriKind.RelativeOrAbsolute);

                        if (linkMapper.IsBlogUrl(url) == true)
                        {
                            var permalink = linkMapper.GetPermalink(url);

                            link.Attributes["href"].Value = permalink.PathAndQuery;
                        }
                    }
                }
            }
        }

        private static void ProcessTechnologyToolboxBlogTables(HtmlDocument doc)
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
            //   <div>{{< table class="small" caption="My Table" >}}</div>
            //   <table>
            //       ...
            //   </table>
            //   <div>{{< /table >}}</div>
            //
            // The custom "table" Hugo shortcode is needed for the following
            // reasons:
            //
            // - Avoid issues where <caption> elements are passed through when
            //   converting from HTML to Markdown
            //
            // - Bootstrap expects <table> elements to have class="table"
            //   attribute
            //
            // - Technology Toolbox blog posts currently contain a number of
            //   tables with many columns -- so to render them within reasonably
            //   wide tables with Bootstrap formatting, the "small" CSS rule
            //   should be applied to all content in the tables

            var elements = doc.DocumentNode.SelectNodes("//table");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var table = element;

                    string caption = null;

                    var captionElement = table.SelectSingleNode(
                        "descendant::caption");

                    if (captionElement != null)
                    {
                        // TODO: Try to preserve links in captions
                        //
                        // e.g. "Table 4" in
                        // https://www.technologytoolbox.com/blog/jjameson/archive/2009/11/02/analyzing-my-msdn-blog.aspx
                        //
                        // Currently, this causes errors when building the site
                        // in Hugo due to quotes being added around the link URL
                        //
                        //caption = captionElement.InnerHtml.Trim();

                        caption = captionElement.InnerText.Trim();

                        captionElement.Remove();
                    }

                    // Insert <div> containing Hugo shortcode
                    // "{{< table ... >}}" before the <table> element

                    var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("div")
                        .WithName("table")
                        .WithParameter("class", "small");

                    if (string.IsNullOrWhiteSpace(caption) == false)
                    {
                        shortcodeBuilder.WithParameter("caption", caption);
                    }

                    var shortcodeNode =shortcodeBuilder.Build();

                    element.ParentNode.InsertBefore(shortcodeNode, element);

                    // Insert <div> containing Hugo shortcode
                    // "{{< /table >}}" after the <table> element

                    shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    shortcodeNode = shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("div")
                        .WithName("/table")
                        .Build();

                    element.ParentNode.InsertAfter(shortcodeNode, element);
                }
            }
        }

        private static void ProcessTechnologyToolboxBlogTableCells(HtmlDocument doc)
        {
            // Normalize whitespace in "simple" <td> content to fix a number of
            // issues during the Markdown process
            //
            // For example, the line breaks in the last column of the table in
            // the following blog post result in "corruption" when converting
            // from HTML to Markdown:
            //
            // https://www.technologytoolbox.com/blog/jjameson/archive/2012/02/19/html-to-pdf-converters.aspx

            var elements = doc.DocumentNode.SelectNodes("//td");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    // Ignore table cells containing HTML line breaks and other
                    // "block" content (e.g. lists)

                    if (element.Descendants("br").Any() == false
                        && element.Descendants("ol").Any() == false
                        && element.Descendants("ul").Any() == false)
                    {
                        HtmlDocumentHelper.NormalizeWhitespaceInChildTextNodes(
                            element);
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
                        var imgSrc = img.GetAttributeValue("src", null);
                        var imgAlt = img.GetAttributeValue("alt", null);
                        var imgHeight = img.GetAttributeValue("height", null);
                        var imgWidth = img.GetAttributeValue("width", null);
                        var imgTitle = img.GetAttributeValue("title", null);

                        if (string.IsNullOrWhiteSpace(captionText) == false)
                        {
                            imgTitle = captionText;
                        }

                        var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        var shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(img.OwnerDocument)
                            .WithHtmlNodeName("div")
                            .WithName("figure")
                            .WithParameter("src", imgSrc)
                            .WithParameter("alt", imgAlt)
                            .WithParameter("height", imgHeight)
                            .WithParameter("width", imgWidth)
                            .WithParameter("title", imgTitle)
                            .WithParametersOnSeparateLines()
                            .Build();

                        img.ParentNode.InsertBefore(shortcodeNode, img);

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

            var elements = doc.DocumentNode.SelectNodes("//kbd");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var content = element.InnerText.Trim();

                    var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    var shortcodeNode = shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("span")
                        .WithName("kbd")
                        .WithParameter(content)
                        .Build();

                    element.ParentNode.InsertBefore(shortcodeNode, element);

                    element.Remove();
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

                    var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    shortcodeBuilder
                        .ForHtmlDocument(doc)
                        .WithName("reference")
                        .WithParameter("title", title)
                        .WithParameter("linkHref", linkHref);

                    if (linkText != linkHref)
                    {
                        shortcodeBuilder
                            .WithParameter("linkText", linkText);
                    }

                    var shortcodeNode = shortcodeBuilder
                        .Build();

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
                        shortcodeNode,
                        citeElement);

                    // Remove the <cite> and <div class="referenceLink">
                    // elements (since these have been replaced by the new
                    // "{{< reference ... >}}" shortcode
                    citeElement.Remove();
                    referenceLink.Remove();
                }
            }
        }

        private static void ReplaceTechnologyToolboxBlogSampElements(
            HtmlDocument doc)
        {
            // Replace simple inline content like the following:
            //
            //    <samp>...</samp>
            //
            // with:
            //
            //    {{< sample-output "..." >}}
            //
            // For multi-line <samp> content similar to the following:
            //
            //   <samp>Line 1<br>
            //   <br>
            //   Line 3</samp>
            //
            // wrap the content with a custom Hugo shortcode, change the <samp>
            // element to <p>, and convert line breaks from HTML to Markdown:
            //
            //   {{< sample-block }}
            //
            //   <p>Line 1\
            //   \
            //   Line 3</p>
            //
            //   {{< /sample-block }}

            var elements = doc.DocumentNode.SelectNodes("//samp");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    var content = element.InnerHtml;

                    // Fix minor inconsistencies in blog posts where <kbd>
                    // elements should have been used instead of <samp> elements
                    if (content == "iisapp.vbs"
                        || content == "stsadm.exe -o activatefeature"
                        || content == "stsadm.exe -o deleteweb"
                        || content == "stsadm.exe -o extendvs"
                        || content == "stsadm.exe -o upgradesolution"
                        || content == "tf.exe checkin"
                        || content == "/bypass")
                    {
                        var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        var shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(element.OwnerDocument)
                            .WithHtmlNodeName("span")
                            .WithName("kbd")
                            .WithParameter(content)
                            .Build();

                        element.ParentNode.InsertBefore(shortcodeNode, element);

                        element.Remove();
                    }
                    else if (content.Contains("\n") == false
                        && content.Contains("<br>") == false)
                    {
                        // Replace <samp>...</samp> with
                        // {{< sample-output "..." >}}

                        var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        var shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(element.OwnerDocument)
                            .WithHtmlNodeName("span")
                            .WithName("sample-output")
                            .WithParameter(content)
                            .Build();

                        element.ParentNode.InsertBefore(shortcodeNode, element);

                        element.Remove();
                    }
                    else if (element.Ancestors("pre").FirstOrDefault() != null)
                    {
                        // Skip <samp> elements inside <pre> elements, since they
                        // are converted to fenced-code blocks in Markdown
                        // (i.e. the <samp> tags are considered obsolete as a
                        // result of the conversion to Markdown)
                        continue;
                    } 
                    else
                    {
                        // Ignore <samp> elements inside <div class="logExcerpt">
                        // elements
                        var divNode = element.Ancestors("div").FirstOrDefault();

                        if (divNode != null)
                        {
                            var divClass = divNode.GetAttributeValue(
                                "class", null);

                            if (divClass == "logExcerpt")
                            {
                                continue;
                            }
                        }

                        // Insert paragraph containing Hugo shortcode
                        // "{{< sample-block >}}" before the <samp> element

                        var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        var shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(element.OwnerDocument)
                            .WithHtmlNodeName("div")
                            .WithName("sample-block")
                            .Build();

                        var paragraph = element.OwnerDocument.CreateElement("p");

                        paragraph.ChildNodes.Add(shortcodeNode);

                        element.ParentNode.InsertBefore(paragraph, element);

                        // Insert paragraph containing Hugo shortcode
                        // "{{< /sample-block >}}" after the <samp> element

                        shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(element.OwnerDocument)
                            .WithHtmlNodeName("div")
                            .WithName("/sample-block")
                            .Build();

                        paragraph = element.OwnerDocument.CreateElement("p");

                        paragraph.ChildNodes.Add(shortcodeNode);

                        element.ParentNode.InsertAfter(paragraph, element);

                        // Change the <samp> element to <p>
                        Debug.Assert(element.Name == "samp");
                        element.Name = "p";

                        // Convert line breaks from HTML to Markdown
                        element.InnerHtml = element.InnerHtml.Replace(
                            "<br>", "\\");
                    }
                }
            }
        }
    }
}
