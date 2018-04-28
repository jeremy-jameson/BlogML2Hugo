using System;
using System.Collections.Generic;
using Html2Markdown.Replacement;
using Html2Markdown.Scheme;

namespace BlogML2Hugo
{
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
}