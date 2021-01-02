using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ThumbnailService
{
    public class ConfigSettings
    {
        public string DefaultWatchFolder { get; set; }
        public string DefaultFormat { get; set; }
        public int DefaultMaxHeight { get; set; }
        public int DefaultMaxWidth { get; set; }
        public string DefaultOutputFolder { get; set; }
        public string DropFolderRegex { get; set; }
        public EventLog Logger { get; set; }
        public string OutputFolderRoot { get; set; }
        public int SleepSeconds { get; set; }
        public string WatchFolderRoot { get; set; }

        public override string ToString()
        {
            Type thistype = this.GetType();

            List<string> propertyValues = new List<string>();

            foreach (var property in thistype.GetProperties())
            {
                propertyValues.Add($"\"{property.Name}\": \"{property.GetValue(this, null).ToString().Replace("\\", "\\\\")}\"");
            }
            return $"{{\"{thistype.Name}\": {{ {(string.Join(",", propertyValues))} }} }}";
        }
    }
}
