using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logistic.Integration.Library.Logistics
{
    [Serializable]
    public class DeviceImportRecord
    {
        public string SerialNumber;
        public string DeviceType;
        public string ModelName;
        public string StockHandlerType;
        public string LocationID;
        public string DeviceStatusCode;
        public string ChipsetID;
        public string SmartCardSerialToPair;
        public string SmartCardToPairModelName;
        public string CustomFields;
        public int recordNumber;
    }
}
