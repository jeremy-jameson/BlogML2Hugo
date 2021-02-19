using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class PostConversionData
    {
        public List<string> Aliases { get; private set; }

        public string Slug { get; set; }

        public string Subfolder { get; set; }

        public List<string> Tags { get; private set; }

        public PostConversionData()
        {
            Aliases = new List<string>();
            Tags = new List<string>();
        }
    }
}
