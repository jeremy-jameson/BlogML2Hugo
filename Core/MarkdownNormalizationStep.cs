using BlogML;
using BlogML.Xml;
using Markdig;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using System.IO;
using System.Linq;

namespace BlogML2Hugo.Core
{
    public class MarkdownNormalizationStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var markdown = postConversionData.Post.Content.UncodedText;

            var document = Markdown.Parse(markdown);

            RemoveTrailingWhitespaceFromFencedCodeBlocks(document);

            using (var stringWriter = new StringWriter())
            {
                var renderer = new NormalizeRenderer(stringWriter);

                var pipeline = new MarkdownPipelineBuilder()
                    .Build();

                pipeline.Setup(renderer);
                renderer.Render(document);
                stringWriter.Flush();

                markdown = stringWriter.ToString();
            }

            markdown = RemoveTrailingSpacesFromEmptyBlockquoteLines(
                markdown);

            postConversionData.Post.Content = BlogMLContent.Create(
                markdown,
                ContentTypes.Text);
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

        private static void RemoveTrailingWhitespaceFromFencedCodeBlock(FencedCodeBlock block)
        {
            for (int i = 0; i < block.Lines.Count; i++)
            {
                block.Lines.Lines[i].Slice.TrimEnd();
            }
        }

        private static void RemoveTrailingWhitespaceFromFencedCodeBlocks(
            MarkdownDocument document)
        {
            document.Descendants<FencedCodeBlock>()
                .ToList()
                .ForEach(block =>
                    RemoveTrailingWhitespaceFromFencedCodeBlock(block)
                );
        }
    }
}
