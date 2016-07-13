using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public class FtpWatcherConfiguration
    {
        public string Name;

        // FTP values.
        public string[] PollingFileExtensions;
        public FtpEndpoint PollingEndpoint;

        // Storage values.
        public string StorageFilePath;

        // Forwarding values.
        //public IEndpoint ForwardingEndpoint;
        public bool DeleteAfterDownloading;
        public object Manager;
    }

    public class FtpEndpoint : IEndpoint
    {
        #region Fields

        public string Address;
        public string Username;
        public string Password;
        public TimeSpan TransferInterval;
        public string InTransitFileExtension;

        #endregion

        public FtpEndpoint()
        {
            //init FtpEndpoint
        }

        public string Name { get; set; }
        public string ConsumerTypename { get; set; }
        public string Settings { get; set; }

        public override string ToString()
        {
            return string.Format
            (
                "Name: {0}\r\nAddress: {1}\r\nUsername: {2}\r\nPassword: {3}\r\nTransferInterval: {4}\r\nInTransitFileExtension: {5}",
                Name, Address, Username, "(hidden)", TransferInterval, InTransitFileExtension
            );
        }
    }

    public interface IEndpoint
    {
        /// <summary>
        /// Gets or sets the name of this endpoint.
        /// </summary>
        /// <value>The name.</value>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the consumer typename.
        /// </summary>
        /// <value>The consumer typename.</value>
        string ConsumerTypename { get; set; }

        /// <summary>
        /// Gets or sets the EndPoint Settings.
        /// </summary>
        string Settings { get; set; }
    }
}
