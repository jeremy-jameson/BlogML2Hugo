using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BlogML2Hugo.Core
{
    public class CreateBlogArchivePagesStep : IBlogPostConversionStep
    {
        public void Execute(BlogPostConversionData postConversionData)
        {
            var post = postConversionData.Post;
            var subfolder = postConversionData.Subfolder;

            var folderParts = postConversionData.Subfolder.Split(
                new char[] { '\\' });

            if (folderParts.Length != 3)
            {
                throw new ArgumentException(
                    "Subfolder must be year/month/day.",
                    "postConversionData");
            }

            var parsedDate = DateTime.ParseExact(
                subfolder.Replace('\\', '/'),
                @"yyyy/MM/dd",
                CultureInfo.InvariantCulture);

            var postDate = post.DateCreated;

            if (parsedDate.Year != postDate.Year
                || parsedDate.Month != postDate.Month
                || parsedDate.Day != postDate.Day)
            {
                throw new ArgumentException(
                    $"Subfolder ({subfolder}) does not match date for post"
                        + $" ({post.DateCreated}).",
                    "postConversionData");
            }

            var postDir = Path.Combine(
                postConversionData.OutputDirectory,
                postConversionData.Subfolder);

            if (Directory.Exists(postDir) == false)
            {
                Directory.CreateDirectory(postDir);
            }

            var dayFolder = postDir;
            var title = $"{postDate.ToString("MMMM d, yyyy")} Blog Posts";
            var date = new DateTime(postDate.Year, postDate.Month, postDate.Day,
                0, 0, 0, DateTimeKind.Local);

            WriteBlogArchivePage(dayFolder, title, date);

            var monthFolder = Directory.GetParent(dayFolder).FullName;
            title = $"{postDate.ToString("MMMM yyyy")} Blog Posts";
            date = new DateTime(postDate.Year, postDate.Month, 1,
                0, 0, 0, DateTimeKind.Local);

            WriteBlogArchivePage(monthFolder, title, date);

            var yearFolder = Directory.GetParent(monthFolder).FullName;
            title = $"{postDate.ToString("yyyy")} Blog Posts";
            date = new DateTime(postDate.Year, 1, 1,
                0, 0, 0, DateTimeKind.Local);

            WriteBlogArchivePage(yearFolder, title, date);
        }

        static void WriteBlogArchivePage(
            string outDir,
            string title,
            DateTime date)
        {
            var outputFile = Path.Combine(
                outDir,
                "_index.md");

            if (File.Exists(outputFile) == true)
            {
                File.Delete(outputFile);
            }

            var sb = new StringBuilder("---");
            sb.AppendLine();

            var escapedTitle = title.Replace("\"", "\\\"");

            sb.AppendLine($"title: \"{escapedTitle}\"");
            sb.AppendLine($"date: {date.ToLocalTime():yyyy-MM-ddTHH:mm:ssK}");

            sb.AppendLine("---");

            var frontMatter = sb.ToString();

            File.WriteAllText(outputFile, frontMatter);
        }
    }
}
