using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThumbnailService
{
    public class ConfigSettings
    {
        public string WatchFolder { get; set; }
        public string OutputFolder { get; set; }
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }

    }
}
