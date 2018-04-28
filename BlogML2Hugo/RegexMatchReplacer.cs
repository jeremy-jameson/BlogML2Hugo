using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Html2Markdown.Replacement;

namespace BlogML2Hugo
{
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
}