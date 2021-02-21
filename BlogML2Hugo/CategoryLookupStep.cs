using BlogML.Xml;
using System;
using System.Collections.Generic;

namespace BlogML2Hugo
{
    public class CategoryLookupStep : IBlogPostConversionStep
    {
        private readonly Dictionary<string, CategoryRef> _categories;

        public CategoryLookupStep(
            Dictionary<string, CategoryRef> categories)
        {
            if (categories == null)
            {
                throw new ArgumentNullException("categories");
            }

            _categories = categories;
        }

        public void Execute(PostConversionData postConversionData)
        {
            var post = postConversionData.Post;

            foreach (BlogMLCategoryReference category in post.Categories)
            {
                var cat = _categories[category.Ref];
                postConversionData.Categories.Add(cat.Title);
            }
        }
    }
}
