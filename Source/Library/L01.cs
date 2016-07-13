﻿using System;
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
    public class L01 : IFComponent, IComponentInitContext
    {
        public IMessageAction Process(IMsgContext msgContext)
        {
            Thread.Sleep(10000);
            var ftpWatcher = new FtpWatcherHelper();
            var instance = new L_01_UploadDevicesAndPairing(msgContext);
            ftpWatcher.OnFileReceived += new FtpWatcherHelper.ProcessFileReceived(instance.Execute);
            ftpWatcher.RequestStart();

            return MessageAction.ContinueProcessing;
        }

        public IReadOnlyPropertySet InitContext { get; }
        public IReadOnlyPropertySet Config { get; }
        public IServices Services { get; }
    }
}
