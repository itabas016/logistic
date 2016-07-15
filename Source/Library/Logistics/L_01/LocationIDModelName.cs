using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    [Serializable]
    public class LocationIDModelName
    {
        public string LocationID;
        public string ModelName;
        public string DeviceStatusCode;

        public override string ToString()
        {
            return string.Format("{0}|{1}|{2}", LocationID, ModelName, DeviceStatusCode);
        }
    }
}
