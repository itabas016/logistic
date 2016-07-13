using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PayMedia.ApplicationServices.SharedContracts;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    [Serializable]
    public class UpdateCustomFieldParams
    {
        public DeviceImportRecord deviceImportRecord;
        public CustomFieldValueCollection customFieldValueCollection;
        public AutoResetEvent doneProcessing;
    }
}
