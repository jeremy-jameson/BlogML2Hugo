using System.Text.RegularExpressions;
using Html2Markdown.Replacement;

namespace BlogML2Hugo
{
    internal class RegexReplacer : IReplacer
    {
        public string Pattern { get; set; }

        public string Replacement { get; set; }

        public string Replace(string html)
        {
            return new Regex(this.Pattern).Replace(html, this.Replacement);
        }
    }
}