using BlogML;
using BlogML.Xml;
using BlogML2Hugo.Core;
using HtmlAgilityPack;
using System;
using System.Diagnostics;
using System.Linq;

namespace TechnologyToolbox.SubtextToHugoBlogConversion
{
    public class TechnologyToolboxBlogPostPreprocessor : IBlogPostConversionStep
    {
        private readonly LinkMapper _linkMapper;

        public TechnologyToolboxBlogPostPreprocessor(
            LinkMapper linkMapper)
        {
            if (linkMapper == null)
            {
                throw new ArgumentNullException("linkMapper");
            }

            _linkMapper = linkMapper;
        }

        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            FixBlogPostDates(post);

            var postHtml = post.Content.UncodedText;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(postHtml);
            {
                RemoveTagsFromPostContent(htmlDoc);
            }

            postHtml = htmlDoc.DocumentNode.OuterHtml;

            post.Content = BlogMLContent.Create(postHtml, ContentTypes.Html);

            var postUrl = new Uri(post.PostUrl);

            // Organize blog posts by year/month/day

            postConversionData.Subfolder =
                $"{post.DateCreated.ToLocalTime():yyyy-MM-dd}"
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
                    postConversionData.Subfolder.Replace("\\", "/"),
                    "/",
                    postConversionData.Slug,
                    ".aspx"));

            postConversionData.Aliases.Add(postUrl.PathAndQuery);

            if (post.PostUrl != url.AbsoluteUri)
            {
                post.PostUrl = url.AbsoluteUri;

                postConversionData.Aliases.Add(url.PathAndQuery);
            }

            _linkMapper.Add(url);

            ProcessBlogPost(htmlDoc);

            post.Content = BlogMLContent.Create(
                htmlDoc.DocumentNode.OuterHtml,
                ContentTypes.Html);
        }

        private static void FixBlogPostDates(BlogMLPost post)
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

        private void ProcessBlogPost(HtmlDocument doc)
        {
            ProcessBlogCallouts(doc);

            ProcessBlogKbdContentInsideConsoleBlocks(doc);

            ProcessBlogConsoleBlocks(doc);
            ProcessBlogLineThroughContent(doc);
            ProcessBlogLogExcerpts(doc);
            ReplaceBlogKbdElements(doc);
            ProcessBlogLinks(doc);
            ProcessBlogTables(doc);
            ReplaceBlogImages(doc);
            ReplaceBlogReferences(doc);
            ReplaceBlogSampElements(doc);
        }

        private static void ProcessBlogCallouts(HtmlDocument doc)
        {
            ProcessBlogDirectQuotes(doc);
            ProcessBlogNotes(doc);
        }

        private static void ProcessBlogDirectQuotes(HtmlDocument doc)
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

        private static void ProcessBlogLogExcerpts(HtmlDocument doc)
        {
            // Replaces blog post content similar to the following:
            //
            //   <div class="logExcerpt">
            //     Lorem ipsum dolor sit amet<br />
            //     consectetur adipiscing elit...
            //   </div>
            //
            // with:
            //
            //   <p>{{< log-excerpt >}}</p>
            //   <div class="logExcerpt"><p> Lorem ipsum dolor sit amet\<br /> consectetur adipiscing elit... </p>
            //   </div>
            //   <p>{{< /log-excerpt >}}</p>

            var elements = doc.DocumentNode.SelectNodes(
                "//div[contains(@class, 'logExcerpt')]");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    bool hasParagraphContent = element.Descendants("p").Any();
                    bool hasPreformattedContent = element.Descendants("pre").Any();

                    if (hasParagraphContent == false
                        && hasPreformattedContent == false)
                    {
                        // Note that <div> elements containing only text and
                        // HTML line breaks -- or more specifically no <p> or
                        // <pre> elements -- are not correctly converted to
                        // Markdown because the combination of HTML line breaks
                        // and "insignificant whitespace" (e.g. "\r\n\t") cause
                        // multiple paragraphs to be produced in the Markdown.
                        //
                        // To prevent this, wrap the content in a new <p>
                        // element, add Markdown line breaks before the HTML
                        // line breaks, and normalize whitespace in the #text
                        // elements.

                        var newElement = doc.CreateElement("p");

                        element.ChildNodes.ToList().ForEach(childNode =>
                        {
                            if (childNode.Name == "#text"
                                && childNode.NextSibling != null
                                && childNode.NextSibling.Name == "br")
                            {
                                var lineBreakNode = childNode.NextSibling;

                                if (lineBreakNode.NextSibling != null
                                    && lineBreakNode.NextSibling.Name == "#text")
                                {
                                    // Normalize whitespace in text following HTML
                                    // line break to ensure the content is treated
                                    // as a single paragraph when subsequently
                                    // converted to Markdown

                                    var textNodeFollowingLineBreakNode =
                                        lineBreakNode.NextSibling;

                                    textNodeFollowingLineBreakNode.InnerHtml =
                                        HtmlDocumentHelper.NormalizeWhitespace(
                                            textNodeFollowingLineBreakNode.InnerHtml);
                                }

                                // Normalize whitespace in text and add backslash
                                // before HTML line break so that subsequent
                                // conversion to Markdown is formatted as expected
                                childNode.InnerHtml =
                                    HtmlDocumentHelper.NormalizeWhitespace(
                                        childNode.InnerHtml) + "\\";
                            }

                            childNode.Remove();

                            newElement.AppendChild(childNode);
                        });

                        element.AppendChild(newElement);
                    }

                    // Insert paragraph containing Hugo shortcode
                    // "{{< log-excerpt >}}" before the
                    // <div class="logExcerpt"> element

                    var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    var shortcodeNode = shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("div")
                        .WithName("log-excerpt")
                        .Build();

                    var paragraph = element.OwnerDocument.CreateElement("p");

                    paragraph.ChildNodes.Add(shortcodeNode);

                    element.ParentNode.InsertBefore(paragraph, element);

                    // Insert paragraph containing Hugo shortcode
                    // "{{< /log-excerpt >}}" after the
                    // <div class="logExcerpt"> element

                    shortcodeBuilder = new HugoShortcodeNodeBuilder();

                    shortcodeNode = shortcodeBuilder
                        .ForHtmlDocument(element.OwnerDocument)
                        .WithHtmlNodeName("div")
                        .WithName("/log-excerpt")
                        .Build();

                    paragraph = element.OwnerDocument.CreateElement("p");

                    paragraph.ChildNodes.Add(shortcodeNode);

                    element.ParentNode.InsertAfter(paragraph, element);
                }
            }
        }

        private static void ProcessBlogNotes(HtmlDocument doc)
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
                    var elementClass = element.GetAttributeValue("class", null);

                    if (elementClass != "noteBody")
                    {
                        continue;
                    }

                    var noteBody = element;

                    if (noteBody.FirstChild != null
                        && noteBody.FirstChild.Name == "#text"
                        && noteBody.ChildNodes.Count() == 1)
                    {
                        var newElement = doc.CreateElement("p");

                        noteBody.ChildNodes.ToList().ForEach(childNode =>
                        {
                            childNode.Remove();

                            newElement.AppendChild(childNode);
                        });

                        //HtmlDocumentHelper.NormalizeWhitespaceInChildTextNodes(
                        //    newElement);

                        noteBody.AppendChild(newElement);
                    }
                }
            }
        }

        private static void ProcessBlogConsoleBlocks(HtmlDocument doc)
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

        private static void ProcessBlogLineThroughContent(HtmlDocument doc)
        {
            // Replace simple inline content like the following:
            //
            //    <span style="text-decoration: line-through">...</span>
            //
            // with:
            //
            //    <span style="text-decoration: line-through">~~...~~</span>
            //
            // For multi-line content similar to the following:
            //
            //   <div>
            //     <ul>...</ul>
            //   </div>
            //
            // wrap the content with a custom Hugo shortcode that will insert
            // <del> elements when the Markdown is subsequently converted to
            // HTML:
            //
            //   <p>{{< deleted-block }}<p>
            //   <div>
            //     <ul>...</ul>
            //   </div>
            //   <p>{{< /deleted-block }}</p>

            var elements = doc.DocumentNode.SelectNodes(
                "//*[contains(@style, 'line-through')]");

            if (elements != null)
            {
                foreach (var element in elements)
                {
                    if (element.Name == "span")
                    {
                        // For inline content, surround the <span> element with
                        // "~~" (which, after converting to Markdown, will be
                        // replaced by Hugo with <del> tags
                        element.ParentNode.InsertBefore(
                            doc.CreateTextNode("~~"),
                            element);

                        element.ParentNode.InsertAfter(
                            doc.CreateTextNode("~~"),
                            element);
                    }
                    else
                    {
                        // For block content insert paragraph containing Hugo
                        // shortcode "{{< deleted-block >}}" before the
                        // "line-through" element

                        var shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        var shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(element.OwnerDocument)
                            .WithHtmlNodeName("div")
                            .WithName("deleted-block")
                            .Build();

                        var paragraph = element.OwnerDocument.CreateElement("p");

                        paragraph.ChildNodes.Add(shortcodeNode);

                        element.ParentNode.InsertBefore(paragraph, element);

                        // Insert paragraph containing Hugo shortcode
                        // "{{< /deleted-block >}}" after the "line-through" element

                        shortcodeBuilder = new HugoShortcodeNodeBuilder();

                        shortcodeNode = shortcodeBuilder
                            .ForHtmlDocument(element.OwnerDocument)
                            .WithHtmlNodeName("div")
                            .WithName("/deleted-block")
                            .Build();

                        paragraph = element.OwnerDocument.CreateElement("p");

                        paragraph.ChildNodes.Add(shortcodeNode);

                        element.ParentNode.InsertAfter(paragraph, element);
                    }
                }
            }
        }

        private static void ProcessBlogKbdContentInsideConsoleBlocks(HtmlDocument doc)
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

                    if (parent.ChildNodes.Count == 2
                        && parent.ChildNodes[0].Name == "#text"
                        && string.IsNullOrWhiteSpace(
                            parent.ChildNodes[0].InnerHtml) == true
                        && parent.ChildNodes[1].Name == "kbd")
                    {
                        // The console block contains only a single <kbd>
                        // element (preceded by some insignificant whitespace).
                        // This is likely a single line command like:
                        //
                        //   SetupWarehouse.exe -o -s beast -d TfsWarehouse ...
                        //
                        // Rather than converting this to a fenced-code block
                        // (by inserting a <pre> element) which would require
                        // horizontal scrolling to view a long line of text,
                        // change the <kbd> element to <p> so the text in the
                        // console block can wrap onto multiple lines.
                        //
                        // Example blog post where this occurs:
                        //
                        // https://www.technologytoolbox.com/blog/jjameson/archive/2010/02/28/lessons-learned-moving-tfs-to-windows-server-2008-and-sql-server-2008.aspx
                        //
                        // Check the length of the <kbd> content. If it is
                        // relatively long -- say, more than 50 characters --
                        // then convert it to a <p> element rather than wrapping
                        // it in a <pre> element.
                        //
                        // For "short" commands, converting to fenced-code
                        // blocks in Markdown is preferred because it preserves
                        // the correct indentation level for content in lists --
                        // such as in the following blog post:
                        //
                        // https://www.technologytoolbox.com/blog/jjameson/archive/2010/12/02/web-standards-design-with-sharepoint-part-2.aspx

                        if (element.InnerHtml.Length > 50)
                        {
                            element.Name = "p";
                            continue;
                        }
                    }

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

        private void ProcessBlogLinks(HtmlDocument doc)
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

                        if (_linkMapper.IsBlogUrl(url) == true)
                        {
                            var permalink = _linkMapper.GetPermalink(url);

                            link.Attributes["href"].Value = permalink.PathAndQuery;
                        }
                    }
                }
            }
        }

        private static void ProcessBlogTables(HtmlDocument doc)
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

                    var shortcodeNode = shortcodeBuilder.Build();

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

        private static void ReplaceBlogImages(HtmlDocument doc)
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
                        var imgClass = img.GetAttributeValue("class", null);
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
                            .WithParameter("class", imgClass)
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

        private static void ReplaceBlogKbdElements(
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

        private static void ReplaceBlogReferences(HtmlDocument doc)
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

        private static void ReplaceBlogSampElements(
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
