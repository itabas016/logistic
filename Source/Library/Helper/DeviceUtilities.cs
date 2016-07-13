using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PayMedia.ApplicationServices.Devices.ServiceContracts;
using PayMedia.ApplicationServices.Devices.ServiceContracts.DataContracts;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public static class DeviceUtilities
    {
        /// <summary>
        /// GetDeviceBySerialNumber
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns>Device</returns>
        public static Device GetDeviceBySerialNumber(string serialNumber)
        {
            Device device = TryGetDeviceBySerialNumber(serialNumber);
            if (device == null)
                throw new IntegrationException(string.Format("Device not found by serial number {0}.", serialNumber));

            return device;
        }

        /// <summary>
        /// TryGetDeviceBySerialNumber
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns>Device if found, otherwise null.</returns>
        public static Device TryGetDeviceBySerialNumber(string serialNumber)
        {
            IDevicesService service = ServiceUtilities.GetService<IDevicesService>();
            Device device = service.GetDeviceBySerialNumber(serialNumber);

            return device;
        }

    }
}
