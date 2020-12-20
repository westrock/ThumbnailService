using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ThumbnailService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ConfigSettings settings = new ConfigSettings()
            {
                MaxHeight = ConfigurationManager.AppSettings["MaxHeight"].Int(),
                MaxWidth = ConfigurationManager.AppSettings["MaxWidth"].Int(),
                OutputFolder =  ConfigurationManager.AppSettings["OutputFolder"],
                WatchFolder = ConfigurationManager.AppSettings["WatchFolder"],
            };

#if DEBUG
            SkiaImageFactory.MainLoop(settings);
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new ThumbnailService(settings)
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
