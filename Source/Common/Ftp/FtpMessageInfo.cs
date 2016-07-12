using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logistic.Integration.Common
{
    /// <summary>
    /// The FtpMessageInfo class contains all the information needed to send along to the ProcessFile function
    /// </summary>
    public class FtpMessageInfo : IFileMessageInfo
    {
        private string filePath;
        private string ftpUrl;
        private string extension;
        private long filesize;

        public FtpMessageInfo()
        {
        }

        /// <summary>
        /// Initializes this endpoint
        /// </summary>
        /// <param name="fileInfo">fileInfo</param>
        /// <param name="filePath">filePath</param>
        /// <param name="ftpUrl">ftpUrl</param>
        public FtpMessageInfo(IFtpFileInfo fileInfo, string filePath, string ftpUrl)
        {
            this.filePath = filePath;
            this.extension = fileInfo.Extension;
            this.filesize = fileInfo.Size;
            this.ftpUrl = ftpUrl;
        }

        public string FilePath
        {
            get { return filePath; }
            set { filePath = value; }
        }
        public string Extension
        {
            get { return extension; }
        }
        public long FileSize
        {
            get { return filesize; }
        }
        public string FtpUrl
        {
            get { return ftpUrl; }
        }
    }

    public interface IFileMessageInfo
    {
        string Extension { get; }
        string FilePath { get; }
    }
}
