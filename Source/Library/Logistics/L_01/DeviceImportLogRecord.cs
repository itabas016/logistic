using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
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
