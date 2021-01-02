using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ThumbnailService
{
    public class WatchFolderSettings
    {
        public string Format { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public string FullName { get; set; }
        public string ActualNameSpec { get; set; }
        public string NameSpec => $"_H{Height}_W{Width}_{Format}";


public WatchFolderSettings() { }

        public WatchFolderSettings(Match match, ConfigSettings settings)
        {
            Format = match.Groups["format"].Success ? match.Groups["format"].Value : settings.DefaultFormat;
            if (match.Groups["maxdim"].Success)
            {
                Height = Width = match.Groups["maxdim"].Value.Int();
            }
            else
            {
                Height = match.Groups["height"].Value.Int(settings.DefaultMaxHeight);
                Width = match.Groups["width"].Value.Int(settings.DefaultMaxWidth);
            }
            FullName = match.Groups[0].Value;
            ActualNameSpec = match.Groups[1].Value;
        }
    }

}
