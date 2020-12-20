using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThumbnailService
{
    public partial class ThumbnailService : ServiceBase
    {
        ConfigSettings _Settings;
        Thread _ThumbnailMonitorThread;

        public ThumbnailService(ConfigSettings settings)
        {
            InitializeComponent();
            _Settings = settings;
        }

        protected override void OnStart(string[] args)
        {
            _ThumbnailMonitorThread = new Thread(() => SkiaImageFactory.MainLoop(_Settings));
            _ThumbnailMonitorThread.Start();
        }

        protected override void OnStop()
        {
            SkiaImageFactory.Stop();
            _ThumbnailMonitorThread.Join();
        }
    }
}
