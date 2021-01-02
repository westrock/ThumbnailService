using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ThumbnailService
{
    static class Program
    {
        private static EventLog _EventLog = new EventLog("ThumbnailService", Environment.MachineName, "ThumbnailService");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            ConfigSettings settings = new ConfigSettings()
            {
                DefaultWatchFolder = ConfigurationManager.AppSettings["DefaultWatchFolder"],
                DefaultFormat = ConfigurationManager.AppSettings["DefaultFormat"],
                DefaultMaxHeight = ConfigurationManager.AppSettings["DefaultMaxHeight"].Int(144),
                DefaultMaxWidth = ConfigurationManager.AppSettings["DefaultMaxWidth"].Int(144),
                DefaultOutputFolder = ConfigurationManager.AppSettings["DefaultOutputFolder"],
                DropFolderRegex = ConfigurationManager.AppSettings["DropFolderRegex"],
                Logger = _EventLog,
                OutputFolderRoot = ConfigurationManager.AppSettings["OutputFolderRoot"],
                SleepSeconds = ConfigurationManager.AppSettings["SleepSeconds"].Int(5),
                WatchFolderRoot = ConfigurationManager.AppSettings["WatchFolderRoot"],
            };


            if (!ValidateSettings(settings))
            {
                _EventLog.LogMessage("Settings Incomplete or invalid.  Service Terminating", EventLogEntryType.Error);
                return;
            }

            _EventLog.LogMessage($@"Service Starting with Settings:
{settings}", EventLogEntryType.Information);

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

        private static bool ValidateSettings(ConfigSettings settings)
        {
            bool hasErrors = false;

            if (string.IsNullOrWhiteSpace(settings.DefaultFormat))
            {
                settings.DefaultFormat = "jpg";
                _EventLog.LogMessage("App.Config Setting DefaultFormat is null or empty, setting to 'jpg'.");
            }
            if (string.IsNullOrWhiteSpace(settings.DefaultOutputFolder))
            {
                _EventLog.LogMessage("App.Config Setting DefaultFormat is null or empty.", EventLogEntryType.Error);
                hasErrors = true;
            }
            if (string.IsNullOrWhiteSpace(settings.DefaultWatchFolder))
            {
                _EventLog.LogMessage("App.Config Setting DefaultWatchFolder is null or empty.", EventLogEntryType.Error);
                hasErrors = true;
            }
            if (string.IsNullOrWhiteSpace(settings.OutputFolderRoot))
            {
                _EventLog.LogMessage("App.Config Setting OutputFolderRoot is null or empty.", EventLogEntryType.Error);
                hasErrors = true;
            }
            else
            {
                if (!Directory.Exists(settings.OutputFolderRoot))
                {
                    try
                    {
                        Directory.CreateDirectory(settings.OutputFolderRoot);
                        _EventLog.LogMessage($"Created OutputFolderRoot at {settings.OutputFolderRoot}");
                    }
                    catch (Exception ex)
                    {
                        _EventLog.LogMessage($"Error Creating OutputFolderRoot: {ex.Message}", EventLogEntryType.Error);
                        hasErrors = true;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(settings.WatchFolderRoot))
            {
                _EventLog.LogMessage("App.Config Setting WatchFolderRoot is null or empty.", EventLogEntryType.Error);
                hasErrors = true;
            }
            else
            {
                if (!Directory.Exists(settings.WatchFolderRoot))
                {
                    try
                    {
                        Directory.CreateDirectory(settings.WatchFolderRoot);
                        _EventLog.LogMessage($"Created WatchFolderRoot at {settings.WatchFolderRoot}");
                    }
                    catch (Exception ex)
                    {
                        _EventLog.LogMessage($"Error Creating WatchFolderRoot: {ex.Message}", EventLogEntryType.Error);
                        hasErrors = true;
                    }
                }
            }

            return !hasErrors; // If has error (is true) and ValidateSettings has failed, return false.
        }
    }
}
