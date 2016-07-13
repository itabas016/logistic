using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    [Serializable]
    public class DeviceImportRecordThreadParam
    {
        public DeviceImportRecord deviceImportRecord;
        public AutoResetEvent doneProcessing;
    }
}
