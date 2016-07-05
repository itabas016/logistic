using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logistic.Integration.Library.Logistics
{
    [Serializable]
    public class DeviceImportLogRecord
    {
        public int ID;
        public int RunID;
        public int FixedRunID;
        public string SourceURI;
        public string ArchivePath;
        public int SucceededCount;
        public int FailedCount;
    }
}
