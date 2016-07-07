﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logistic.Integration.Library.Logistics;
using PayMedia.Integration.FrameworkService.Common;
using PayMedia.Integration.FrameworkService.Interfaces;
using PayMedia.Integration.FrameworkService.Interfaces.Common;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public class L01 : IFComponent, IComponentInitContext
    {
        public IMessageAction Process(IMsgContext msgContext)
        {
            var instance = new L_01_UploadDevicesAndPairing();
            instance.Execute(msgContext);

            return MessageAction.ContinueProcessing;
        }

        public IReadOnlyPropertySet InitContext { get; }
        public IReadOnlyPropertySet Config { get; }
        public IServices Services { get; }
    }
}
