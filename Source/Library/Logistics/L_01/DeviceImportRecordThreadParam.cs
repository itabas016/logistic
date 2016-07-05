using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Logistic.Integration.Library.Logistics
{
    [Serializable]
    public class DeviceImportRecordThreadParam
    {
        public DeviceImportRecord deviceImportRecord;
        public AutoResetEvent doneProcessing;
    }
}
