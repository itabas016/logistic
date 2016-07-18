using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PayMedia.Integration.IFComponents.BBCL.Logistics;
using PayMedia.Integration.FrameworkService.Common;
using PayMedia.Integration.FrameworkService.Interfaces;
using PayMedia.Integration.FrameworkService.Interfaces.Common;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public class L01 : IFComponent, IComponentInitContext, IDisposable
    {
        private FtpWatcherHelper _ftpWatcher;

        public L01(IComponentInitContext componentInitContext)
        {
            //first load application setting and workersetting from component property
            Configuration.Init(componentInitContext);

            _ftpWatcher = new FtpWatcherHelper(Configuration.FtpWatcherSetting);
            var instance = new L_01_UploadDevicesAndPairing(componentInitContext);
            _ftpWatcher.OnFileReceived += new FtpWatcherHelper.ProcessFileReceived(instance.Execute);
            _ftpWatcher.RequestStart();
        }

        ~L01()
        {
            Configuration.ClearConfiguration();

            _ftpWatcher.ForceStop();
        }

        public void Dispose()
        {
            Configuration.ClearConfiguration();

            _ftpWatcher.RequestStop(1000);
        }

        public IMessageAction Process(IMsgContext msgContext)
        {
            return MessageAction.ContinueProcessing;
        }

        public IReadOnlyPropertySet InitContext { get; }
        public IReadOnlyPropertySet Config { get; }
        public IServices Services { get; }
    }
}
