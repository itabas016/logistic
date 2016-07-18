using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
    /// A wrapper class for .NET 2.0 FTP / SFTP
    /// </summary>
    /// <remarks>
    /// This class does not hold open an FTP connection but
    /// instead is stateless: for each FTP request it
    /// connects, performs the request and disconnects.
    /// </remarks>
    public class FtpClient
    {
        #region "CONSTRUCTORS"
        /// <summary>
        /// Blank constructor
        /// </summary>
        /// <remarks>Hostname, username and password must be set manually</remarks>
        public FtpClient()
        {
        }

        /// <summary>
        /// Constructor just taking the hostname
        /// </summary>
        /// <param name="Hostname">in one of the following forms: ftp://ftp.host.com/path/to/filename.ext, ftp://ftp.host.com, or ftp.host.com</param>
        /// <remarks></remarks>
        public FtpClient(string Hostname)
        {
            setAddressProperties(Hostname);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FTPAddress">in one of the following forms: ftp://ftp.host.com/path/to/filename.ext, ftp://ftp.host.com, or ftp.host.com</param>
        /// <param name="Username">Leave blank to use 'anonymous' but set password to your email</param>
        /// <param name="Password"></param>
        /// <param name="EndpointSettings"></param>
		public FtpClient(string FTPAddress, string Username, string Password)
        {
            _address = FTPAddress;
            setAddressProperties(FTPAddress);
            _username = Username;
            _password = Password;
            //_endPointSettings = LoadEndpointSettings(EndpointSettings);
        }

        #endregion

        #region "Directory functions"
        /// <summary>
        /// Return a simple directory listing
        /// </summary>
        /// <param name="directory">Directory to list, e.g. /pub</param>
        /// <returns>A list of filenames and directories as a List(of String)</returns>
        /// <remarks>For a detailed directory listing, use ListDirectoryDetail</remarks>
        public List<string> ListDirectory(string directory)
        {
            return wodFtpWrapper.ListDirectory(directory);
        }

        /// <summary>
        /// Returns a list of files.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns>A list of filenames and directories as a List(of FtpFileInfo)</returns>
        public List<FtpFileInfo> ListFiles(string directory)
        {
            return wodFtpWrapper.ListFiles(directory);
        }

        /// <summary>
        /// Return a detailed directory listing
        /// </summary>
        /// <param name="directory">Directory to list, e.g. /pub/etc</param>
		/// <returns>An FtpDirectory object</returns>
        public FtpDirectory ListDirectoryDetail(string directory)
        {
            return wodFtpWrapper.ListDirectoryDetail(directory);
        }

        #endregion

        #region "Upload: File transfer TO ftp/sftp server"
        /// <summary>
        /// Copy a local file to the FTP server
        /// </summary>
        /// <param name="localFilename">Full path of the local file</param>
        /// <param name="targetFilename">Target filename, if required</param>
        /// <returns></returns>
        /// <remarks>If the target filename is blank, the source filename is used
        /// (assumes current directory). Otherwise use a filename to specify a name
        /// or a full path and filename if required.</remarks>
        public bool Upload(string localFilename, string targetFilename)
        {
            //1. check source
            if (!File.Exists(localFilename))
            {
                throw (new ApplicationException("File " + localFilename + " not found"));
            }
            //copy to FI
            FileInfo fi = new FileInfo(localFilename);
            return Upload(fi, targetFilename);
        }

        /// <summary>
        /// Upload a local file to the FTP server
        /// </summary>
        /// <param name="fi">Source file</param>
        /// <param name="targetFilename">Target filename (optional)</param>
        /// <returns></returns>
        public bool Upload(FileInfo fi, string targetFilename)
        {
            try
            {
                return wodFtpWrapper.Upload(fi, targetFilename);
            }
            catch (Exception e)
            {
                throw new IntegrationException(string.Format("Failed to upload {0} to {1}.\n\n{2}", FormatPathForSFTP(fi.FullName), _address, e.ToString()));
            }
        }

        private XmlNode LoadEndpointSettings(string settings)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                if (!string.IsNullOrEmpty(settings))
                {

                    doc.LoadXml(settings);
                    return doc.DocumentElement;
                }
                else
                    return doc.DocumentElement;
            }
            catch (Exception ex)
            {
                throw new IntegrationException(string.Format("Invalid Endpoint settings. Please check the endpoint settings  for the Address {0}. Error Message : {1}", Address, ex.Message));
            }
        }
        /// <summary>
        /// Upload a local file to the FTP server
        /// </summary>
        /// <param name="fi">Source file</param>
        /// <param name="targetFilename">Target filename (optional)</param>
        /// <param name="finalFinalName">Final filename (optional)</param>
        /// <returns></returns>
        public bool UploadAndRename(FileInfo fi, string targetFilename, string finalFinalName)
        {
            try
            {
                return wodFtpWrapper.UploadAndRename(fi, targetFilename, finalFinalName);
            }
            catch (Exception e)
            {
                throw new IntegrationException(string.Format("Failed to upload {0} to {1}.\n\n{2}", FormatPathForSFTP(fi.FullName), _address, e.ToString()));
            }
        }
        #endregion

        #region "Download: File transfer FROM ftp/sftp server"
        /// <summary>
        /// Copy a file from FTP/SFTP server to local
        /// </summary>
        /// <param name="sourceFilename">Target filename, if required</param>
        /// <param name="localFilename">Full path of the local file</param>
        /// <param name="PermitOverwrite">If the file exists on the FTP server, can it be overwritten</param>
        /// <returns></returns>
        /// <remarks>Target can be blank (use same filename), or just a filename
        /// (assumes current directory) or a full path and filename</remarks>
        public bool Download(string sourceFilename, string localFilename, bool PermitOverwrite)
        {
            //determine target file
            FileInfo fi = new FileInfo(localFilename);
            return this.Download(sourceFilename, fi, PermitOverwrite);
        }

        //Version taking an FtpFileInfo
        public bool Download(FtpFileInfo file, string localFilename, bool PermitOverwrite)
        {
            return this.Download(file.FullName, localFilename, PermitOverwrite);
        }

        //Another version taking FtpFileInfo and FileInfo
        public bool Download(FtpFileInfo file, FileInfo localFI, bool PermitOverwrite)
        {
            return this.Download(file.FullName, localFI, PermitOverwrite);
        }

        //Version taking string/FileInfo
        public bool Download(string sourceFilename, FileInfo targetFI, bool PermitOverwrite)
        {
            try
            {
                return wodFtpWrapper.Download(sourceFilename, targetFI, PermitOverwrite);
            }
            catch (Exception)
            {
                //delete target file as it's incomplete
                targetFI.Delete();
                throw;
            }
        }

        #endregion

        #region "Other functions: Delete rename etc."
        /// <summary>
        /// Delete remote file
        /// </summary>
        /// <param name="filename">filename or full path</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool FtpDelete(string filename)
        {
            try
            {
                return wodFtpWrapper.FtpDelete(filename);
            }
            catch
            {
                return false;
            }
        }

        public bool ClearCurrentDirectory()
        {
            try
            {
                return wodFtpWrapper.ClearCurrentDirectoryAlt();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determine if file exists on remote FTP/SFTP site
        /// </summary>
        /// <param name="filename">Filename (for current dir) or full path</param>
        /// <returns></returns>
        /// <remarks>Note this only works for files</remarks>
        public bool FtpFileExists(string filename)
        {
            try
            {
                return wodFtpWrapper.FtpFileExists(filename);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Determine size of remote file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /// <remarks>Throws an exception if file does not exist</remarks>
        public long GetFileSize(string filename)
        {
            return wodFtpWrapper.GetFileSize(filename);
        }

        /// <summary>
        /// Rename a file
        /// </summary>
        /// <param name="sourceFilename">Source filename</param>
        /// <param name="newName">New filename</param>
        /// <returns>Boolean indicating outcome of operation</returns>
        public bool FtpRename(string sourceFilename, string newName)
        {
            try
            {
                return wodFtpWrapper.FtpRename(sourceFilename, newName);
            }
            catch (Exception ex)
            {
                Diagnostics.Warning(string.Format("Warning: exception thrown in FtpRename (useSFTP=true) with sourceFilename='{0}', newName={1}:\r\n{2}\r\nStack trace:\r\n{3}", sourceFilename, newName, ex.Message, ex.StackTrace));
                return false;
            }
        }

        public bool FtpCreateDirectory(string dirpath)
        {
            try
            {
                return wodFtpWrapper.FtpCreateDirectory(dirpath);
            }
            catch (Exception)
            {
                return false;
            }

            //return true;
        }

        public bool FtpDeleteDirectory(string dirpath)
        {
            try
            {
                return wodFtpWrapper.FtpDeleteDirectory(dirpath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Public Static Utility Methods
        /// <summary>
        /// Parse address into hosttype, hostname, path, and filename
        /// </summary>
        /// <param name="ftpURI">Input address string</param>
        /// <param name="hosttype">Host type ('ftp://', 'sftp://' or 'ftps://') from <paramref name="ftpURI"/>.</param>
        /// <param name="hostname">Host name or address from <paramref name="ftpURI"/>.</param>
        /// <param name="path">Path portion from <paramref name="ftpURI"/> or an empty string.</param>
        /// <param name="filename">Filename portion of <paramref name="ftpURI"/> or an empty string.</param>
        public static void ParseAddress(string ftpURI, out string hosttype, out string hostname, out string path, out string filename)
        {
            // if there is a host type ('ftp://', 'sftp://' or 'ftps://') verify it is a valid type and grab it.
            if (ftpURI.Contains("//"))
            {
                hosttype = string.Empty;
                foreach (string type in hostTypes)
                {
                    if (ftpURI.StartsWith(type, StringComparison.InvariantCultureIgnoreCase))
                    {
                        hosttype = type;
                        ftpURI = ftpURI.Remove(0, type.Length);
                        break;
                    }
                }
            }
            else // not host type specified, assume ftp://
                hosttype = hostTypes[0];

            if (hosttype.Length == 0)
            {
                string error = string.Format("Error: The FTP Address does NOT contain a valid prefix 'ftp://', 'sftp://' or 'ftps://' . The FTP Address passed in was :'{0}'\r\n", ftpURI);
                throw new IntegrationException(error);
            }

            // see if there is a path in the ftpURI
            int firstSlash = ftpURI.IndexOf('/');
            if (firstSlash > 0)
            {
                // Separate out hostname, path and filename.
                hostname = ftpURI.Substring(0, firstSlash);
                path = ftpURI.Substring(firstSlash);

                // if there is a "." after the last slash then we will assume there is a file name present.
                // if there is a file name present then remove it from the path.
                int lastSlash = path.LastIndexOf('/');
                if (path.Substring(lastSlash).IndexOf('.') >= 0)
                {
                    // get the filename.
                    filename = path.Substring(lastSlash);

                    // Remove filename from path.
                    path = path.Remove(lastSlash);
                }
                else
                    filename = string.Empty;
            }
            else
            {
                // if we made it here then the ftpURI is just the host type (optional) and host name, for example "ftp://fred.com"
                hostname = ftpURI;
                path = string.Empty;
                filename = string.Empty;
            }
        }

        public static string FormatPathForSFTP(string strPath)
        {
            strPath = strPath.Replace("\\", "/");
            if (strPath.StartsWith("//"))
                strPath = strPath.Remove(1, 1);
            return strPath;
        }
        #endregion

        #region "private supporting fns"


        /// <summary>
        /// Parse address into hostname, path
        /// </summary>
        /// <param name="strAddress">Input address string</param>
        /// <returns>hostname in the format: ftp.host.com</returns>
        private string setAddressProperties(string strAddress)
        {
            // 2012.04.10 JCopus The original version of this method use a URIBuilder class. The problem
            // with the URIBuilder is that it incorrectly escapes paths that have spaces with '%20', instead of the
            // "+" that FTP really likes.  So "ftp://fred.com/root foler/filename.txt" gets converted by UriBuilder
            // to "ftp://fred.com/root%20folder/filename.txt".  When it should be "ftp://fred.com/root+folder/filename.txt".
            // This is even more funny since it fails when I try to use a FTP URL that has spaces in the path against my Microsoft FTP server.
            string filename;

            ParseAddress(strAddress, out _hostType, out _hostname, out _currentDirectory, out filename);
            return _hostname;
        }

        /// <summary>
        /// Get the credentials from username/password
        /// </summary>
        private ICredentials GetCredentials()
        {
            return new NetworkCredential(Username, Password);
        }

        /// <summary>
        /// Amend an FTP path so that it always starts with /
        /// </summary>
        /// <param name="path">Path to adjust</param>
        /// <returns></returns>
        /// <remarks></remarks>
        private string AdjustDir(string path)
        {
            return ((path.StartsWith("/")) ? "" : "/").ToString() + path;
        }

        #endregion

        #region "Properties"

        private string _address;

        /// <summary>
        /// The raw address used to initialize this class instance.
        /// </summary>
        public string Address
        {
            get
            {
                return _address;
            }
            set
            {
                _address = value;
            }
        }

        protected static string[] hostTypes = { "ftp://", "sftp://", "ftps://" };

        private string _hostType;
        /// <summary>
        /// The Host type ('ftp://', 'sftp://' or 'ftps://')
        /// </summary>
        public string HostType
        {
            get { return _hostType; }
            set
            {
                bool valid = false;
                foreach (string type in hostTypes)
                {
                    if (value.Equals(type, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _hostType = value;
                        valid = true;
                    }
                }
                if (valid == false)
                {
                    string error = string.Format("Error: Setting the HostType propertiy to '{0}'.  The specified HostType is invalid.  Valid types are 'ftp://', 'sftp://' or 'ftps://'.\r\n", value ?? "<NULL>");
                    throw new IntegrationException(error);
                }
            }
        }

        private string _hostname;
        /// <summary>
        /// Hostname
        /// </summary>
        /// <value></value>
        /// <remarks>The Hostname is always returned in full URL format "ftp://ftp.myhost.com"
        /// <para/>But when setting it if no host type is specified (i.e. "ftp.myhost.com") it will 
        /// assume a host type of "ftp://". If a host type is specified it MUST be a valid type
        /// of 'ftp://', 'sftp://' or 'ftps://'.
        /// </remarks>
        public string Hostname
        {
            get
            {
                return _hostType + _hostname;
            }
            set
            {
                _address = value;
                setAddressProperties(value);
            }
        }
        private string _username;
        /// <summary>
        /// Username property
        /// </summary>
        /// <value></value>
        /// <remarks>Can be left blank, in which case 'anonymous' is returned</remarks>
        public string Username
        {
            get
            {
                return (_username == "" ? "anonymous" : _username);
            }
            set
            {
                _username = value;
            }
        }
        private string _password;
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                _password = value;
            }
        }

        private XmlNode _endPointSettings;
        public XmlNode EndPointSettings
        {
            get
            {
                return _endPointSettings;
            }
        }
        /// <summary>
        /// The CurrentDirectory value
        /// </summary>
        /// <remarks>Defaults to the root '/'</remarks>
        private string _currentDirectory = "/";
        public string CurrentDirectory
        {
            get
            {
                //return directory, ensure it ends with /
                return _currentDirectory + ((_currentDirectory.EndsWith("/")) ? "" : "/");
            }
            set
            {
                if (!value.StartsWith("/"))
                {
                    throw (new ApplicationException("Directory should start with /"));
                }
                _currentDirectory = value;
            }
        }

        private WODFtpWrapper ftpWrapper = null;
        public WODFtpWrapper wodFtpWrapper
        {
            get
            {
                if (ftpWrapper == null)
                    ftpWrapper = new WODFtpWrapper(_address, _username, _password, EndPointSettings);
                ftpWrapper.CurrentDirectory = CurrentDirectory;

                return ftpWrapper;
            }
        }


        #endregion

    }
}
