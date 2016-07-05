using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using WeOnlyDo.Client;

namespace Logistic.Integration.Common
{
    /// <summary>
    /// A wrapper class for FTP
    /// </summary>
    /// <remarks>
    /// This class does not hold open a FTP connection but
    /// instead is stateless: for each FTP request it
    /// connects, performs the request and disconnects.
    /// </remarks>
    public class WODFtpWrapper
    {
        #region WODFtp variables
        private bool m_blnListDirectoryDone = false;
        private bool m_blnListDirectoryDetailsDone = false;
        private bool m_blnGetFileSizeDone = false;
        private bool m_blnUploadDone = false;
        private bool m_blnFtpDeleteDone = false;
        private bool m_blnFtpRenameDone = false;
        private bool m_blnFtpCreateDirectoryDone = false;
        private bool m_blnFtpDeleteDirectoryDone = false;
        private bool m_blnDownloadDone = false;
        private bool m_blnUploadMultipleDone = false;
        private bool m_blnClearCurrentDirectoryDone = false;
        private string m_strListDirectoryDirItems = string.Empty;
        private string m_strListDirectoryDetailsDirItems = string.Empty;
        private string m_strUploadMultipleExtension = string.Empty;
        private string m_strErrorMessage = string.Empty;
        private string m_ftpReplyText = string.Empty;
        private long m_lngGetFileSize_Size = 0;
        private int m_intConnectRetryCount = 0;
        private int m_intConnectTimeOutInSeconds = 0;
        private int m_intOperationTimeOutInSeconds = 0;
        private bool isShipRightFtp = false;
        private static object locker;
        #endregion

        #region "CONSTRUCTORS"
        /// <summary>
        /// Constructor taking hostname, username and password
        /// </summary>
        /// <param name="Address">in one of the following forms: ftp://ftp.host.com/path/to/filename.ext, ftp://ftp.host.com, or ftp.host.com</param>
        /// <param name="Username">Leave blank to use 'anonymous' but set password to your email</param>
        /// <param name="Password"></param>
        /// <param name="EndpointSettings"></param>
        /// <remarks></remarks>
        public WODFtpWrapper(string Address, string Username, string Password, XmlNode EndpointSettings)
        {
            ParseAddress(Address);
            _username = Username;
            _password = Password;
            _endpointSettings = EndpointSettings;

            // 2012.06.26 - if this is the ship right FTP server then we have to do a thing or two differently than we do for everyone else.
            if (EndpointSettings != null)
                bool.TryParse(XmlUtilities.SafeSelectText(EndpointSettings, "//IsShipRightFTP"), out isShipRightFtp);
        }

        static WODFtpWrapper()
        {
            locker = new object();
        }
        #endregion

        #region "Directory functions"

        /// <summary>
        /// Given a FTP path, returns the directory minus the filename.
        /// Example input: /dir/file.txt
        /// Example output: /dir/
        /// </summary>
        /// <param name="path">FTP path, e.g. /dir/file.txt</param>
        /// <returns>FTP directory, without filename</returns>
        private static string GetDirectoryFromFTPPath(string path)
        {
            // Ensure input has forward slashes
            var newPath = path.Replace('\\', '/');

            // Directory starts with last '/'
            return newPath.Substring(0, newPath.LastIndexOf('/') + 1);
        }

        /// <summary>
        /// Given a FTP path, returns the extension.
        /// Example input: /my.dir/my.file.name
        /// Example output: name
        /// </summary>
        /// <param name="path">FTP path, e.g. /my.dir/my.file.name</param>
        /// <returns>Extension, without leading dot</returns>
        private static string GetExtensionFromFTPPath(string path)
        {
            // Ensure input has forward slashes
            var newPath = path.Replace('\\', '/');

            // Get filename, starting with last '/'
            var ext = newPath.Substring(newPath.LastIndexOf('/') + 1);

            // If filename starts with ".", remove it
            if (ext.StartsWith("."))
                ext = ext.Substring(1);

            // If filename contains a ".", take extension starting with last "."
            if (ext.Contains("."))
                ext = ext.Substring(ext.LastIndexOf('.') + 1);
            return ext;
        }

        /// <summary>
        /// Return a simple directory listing
        /// </summary>
        /// <param name="directory">Directory to list, e.g. /pub</param>
        /// <returns>A list of filenames and directories as a List(of String)</returns>
        /// <remarks>For a detailed directory listing, use ListDirectoryDetail</remarks>
        public List<string> ListDirectory(string directory)
        {
            FtpDLX ftp = null;
            var extension = string.Empty;// default

            try
            {
                ftp = GetWODFtpClient();
                ftp.ListItemsEvent += ListDirectory_ftp_ListItemsEvent;
                ftp.DoneEvent += ListDirectory_ftp_DoneEvent;
                if (directory.Contains("*"))
                {
                    extension = GetExtensionFromFTPPath(directory);
                    directory = GetDirectoryFromFTPPath(directory);
                }
                m_blnListDirectoryDone = false;
                m_strListDirectoryDirItems = string.Empty;
                ftp.ListNames(directory);

                var timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnListDirectoryDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        break;
                }
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            // Wildcard handling
            return m_strListDirectoryDirItems.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(
                                              dirItem => dirItem != "../" && dirItem != "./" &&
                                                         (dirItem.EndsWith(extension) || string.IsNullOrEmpty(extension) || extension == "*") && !(dirItem.Contains("/"))).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public List<FtpFileInfo> ListFiles(string directory)
        {
            var fileList = new List<FtpFileInfo>();
            FtpDLX ftp = null;

            try
            {
                ftp = GetWODFtpClient();
                ftp.ListItemsEvent += ListDirectory_ftp_ListItemsEvent;
                ftp.DoneEvent += ListDirectory_ftp_DoneEvent;
                string extension = string.Empty; // default
                if (directory.Contains("*"))
                {
                    extension = GetExtensionFromFTPPath(directory);
                    directory = GetDirectoryFromFTPPath(directory);
                }
                m_blnListDirectoryDone = false;
                m_strListDirectoryDirItems = string.Empty;
                ftp.ListNames(directory);

                var timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnListDirectoryDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        break;
                }

                // Wildcard handling
                var result = new List<string>();
                foreach (var dirItem in m_strListDirectoryDirItems.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(
                    dirItem => dirItem != "../" && dirItem != "./" && (dirItem.EndsWith(extension) || string.IsNullOrEmpty(extension) || extension == "*") && !(dirItem.Contains("/"))))
                {
                    result.Add(dirItem);
                    fileList.Add(new FtpFileInfo(dirItem, directory, false));
                }
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return fileList;
        }

        #region WOD Events

        private void ListDirectory_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnListDirectoryDone = Args.Error == 0;
        }

        private void ListDirectory_ftp_ListItemsEvent(object Sender, FtpListItemsArgs Args)
        {
            //i31688 - this event could be triggered more than once, so here to concatenate the returned filename string respectively.
            m_strListDirectoryDirItems += Args.FileInfo;
        }

        private void ListDirectoryDetails_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnListDirectoryDetailsDone = Args.Error == 0;
        }

        private void ListDirectoryDetails_ftp_ListItemsEvent(object Sender, FtpListItemsArgs Args)
        {
            m_strListDirectoryDetailsDirItems += Args.FileInfo;
        }

        private void GetFileSize_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnGetFileSizeDone = Args.Error == 0;
        }

        private void GetFileSize_ftp_AttributesEvent(object Sender, DirItem Args)
        {
            m_lngGetFileSize_Size = Args.Size;
        }

        private void Upload_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnUploadDone = Args.Error == 0;
        }

        private void FtpDelete_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnFtpDeleteDone = Args.Error == 0;
        }

        private void FtpRename_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnFtpRenameDone = Args.Error == 0;
        }

        private void FtpCreateDirectory_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnFtpCreateDirectoryDone = Args.Error == 0;
        }

        private void FtpDeleteDirectory_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnFtpDeleteDirectoryDone = Args.Error == 0;
        }

        private void Download_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnDownloadDone = Args.Error == 0;
        }

        private void UploadMultiple_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnUploadMultipleDone = Args.Error == 0;
        }

        private void UploadMultiple_ftp_LoopItemEvent(object Sender, FtpLoopArgs Args)
        {
            // if the item is a file and does NOT match our upload extension then skip the file.
            if (Args.ItemType == DirItemTypes.File && Path.GetExtension(Args.LocalFile) != m_strUploadMultipleExtension)
                Args.Skip = true;
        }

        private void ClearCurrentDirectory_ftp_DoneEvent(object Sender, FtpDoneArgs Args)
        {
            m_blnClearCurrentDirectoryDone = Args.Error == 0;
        }

        public void ftp_FtpReplyEvent(object Sender, FtpReplyArgs Args)
        {
            m_ftpReplyText = string.Format("Reply received for the command {0} with the replycode '{1}' and message '{2}'", Args.Command, Args.ReplyCode, Args.ReplyText);
        }
        #endregion

        /// <summary>
        /// Return a detailed directory listing
        /// </summary>
        /// <param name="directory">Directory to list, e.g. /pub/etc</param>
        /// <returns>An FtpDirectory object</returns>
        public FtpDirectory ListDirectoryDetail(string directory)
        {
            FtpDirectory dirlist = null;
            FtpDLX ftp = null;

            try
            {
                ftp = GetWODFtpClient();

                ftp.DoneEvent += ListDirectoryDetails_ftp_DoneEvent;
                ftp.ListItemsEvent += ListDirectoryDetails_ftp_ListItemsEvent;

                var extension = "txt"; // default

                if (directory.Contains("*"))
                {
                    extension = GetExtensionFromFTPPath(directory);
                    directory = GetDirectoryFromFTPPath(directory);
                }

                m_blnListDirectoryDetailsDone = false;
                m_strListDirectoryDetailsDirItems = string.Empty;

                ftp.ListDir(directory);

                var timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnListDirectoryDetailsDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Unable to get directory details for directory '{0}' on host '{1}'.", directory, ftp.Hostname));
                }

                // Wildcard handling
                m_strListDirectoryDetailsDirItems = m_strListDirectoryDetailsDirItems.Replace("./\r\n../\r\n", "");

                m_strListDirectoryDetailsDirItems = string.Join(Environment.NewLine, m_strListDirectoryDetailsDirItems.Split(new[]
                    {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries).Where(
                    strItem => (strItem.EndsWith(extension) || string.IsNullOrEmpty(extension) || extension == "*") && !(strItem.Contains("/"))).ToArray());
                dirlist = new FtpDirectory(m_strListDirectoryDetailsDirItems, directory);
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return dirlist;
        }

        #endregion

        #region "Upload: File transfer TO ftp server"
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
            var fi = new FileInfo(localFilename);
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
            //copy the file specified to target file: target file can be full path or just filename (uses current dir)

            //1. check target
            string target;
            if (targetFilename.Trim() == "")
            {
                //Blank target: use source filename & current dir
                target = this.CurrentDirectory + fi.Name;
            }
            else if (targetFilename.Contains("/"))
            {
                //If contains / treat as a full path
                target = targetFilename;
            }
            else
            {
                //otherwise treat as filename only, use current directory
                target = CurrentDirectory + targetFilename;
            }

            string URI = Hostname + AdjustDir(target);

            // Get timeout from config
            int uploadTimeoutSeconds = GetUploadTimeOut();

            FtpDLX ftp = null;

            try
            {
                target = FormatPathForFTP(target);

                ftp = GetWODFtpClient();
                m_blnUploadDone = false;
                ftp.DoneEvent += new FtpDLX.DoneDelegate(Upload_ftp_DoneEvent);


                ftp.PutFile(fi.FullName, target);

                DateTime timeout = DateTime.Now.AddSeconds(uploadTimeoutSeconds);

                while (!m_blnUploadDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Timeout occurred while uploading {0} to {1} with Useftp set to true.", FormatPathForFTP(fi.FullName), URI));
                }
            }
            catch (Exception e)
            {
                throw new IntegrationException(string.Format("Failed to upload {0} to {1} with Useftp set to true.\n\n{2}", FormatPathForFTP(fi.FullName), URI, e.ToString()));
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;

        }

        /// <summary>
        /// Upload all files in the <paramref name="LocalPath"/> that have the extension <paramref name="Extension"/> 
        /// to the destination FTP folder <paramref name="RemotePath"/>.
        /// </summary>
        /// <param name="LocalPath">Local folder of potential files to upload</param>
        /// <param name="RemotePath">Destination folder for uploaded files</param>
        /// <param name="MaxLevel">How many levels of <paramref name="LocalPath"/> folders to recurse when looking for files with the extension <paramref name="Extension"/> to upload</param>
        /// <param name="Extension">All files found with this extension will be uploaded</param>
        /// <returns></returns>
        public bool UploadMultiple(string LocalPath, string RemotePath, int MaxLevel, string Extension)
        {
            m_strUploadMultipleExtension = Extension;
            m_blnUploadMultipleDone = false;
            FtpDLX ftp = null;

            try
            {
                ftp = GetWODFtpClient();
                ftp.LoopItemEvent += UploadMultiple_ftp_LoopItemEvent;
                ftp.DoneEvent += UploadMultiple_ftp_DoneEvent;
                ftp.PutFiles(LocalPath, RemotePath, MaxLevel);

                // Get timeout from config
                int uploadTimeoutSeconds = GetUploadTimeOut();

                DateTime timeout = DateTime.Now.AddSeconds(uploadTimeoutSeconds);

                while (!m_blnUploadMultipleDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Timeout occurred while uploading mutiple files from directory {0} to {1} with Useftp set to true.", LocalPath, RemotePath));
                }
            }
            catch (Exception e)
            {
                throw new IntegrationException(string.Format("Timeout occurred while uploading mutiple files from directory {0} to {1} with Useftp set to true.\n\n{2}", LocalPath, RemotePath, e.ToString()));
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="LocalPath"></param>
        /// <param name="RemotePath"></param>
        /// <param name="Extension"></param>
        /// <returns></returns>
        public bool UploadMultiple(string LocalPath, string RemotePath, string Extension)
        {
            return UploadMultiple(LocalPath, RemotePath, 1, Extension);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fi"></param>
        /// <param name="targetFilename"></param>
        /// <param name="targetFinalName"></param>
        /// <returns></returns>
        public bool UploadAndRename(FileInfo fi, string targetFilename, string targetFinalName)
        {
            //copy the file specified to target file: target file can be full path or just filename (uses current dir)

            //1. check target
            string target;
            if (targetFilename.Trim() == "")
            {
                //Blank target: use source filename & current dir
                target = this.CurrentDirectory + fi.Name;
            }
            else if (targetFilename.Contains("/"))
            {
                //If contains / treat as a full path
                target = targetFilename;
            }
            else
            {
                //otherwise treat as filename only, use current directory
                target = CurrentDirectory + targetFilename;
            }

            string URI = Hostname + AdjustDir(target);

            // Get timeout from config
            int uploadTimeoutSeconds = GetUploadTimeOut();

            FtpDLX ftp = null;
            bool renameSucceeded = true;

            try
            {
                target = FormatPathForFTP(target);
                ftp = GetWODFtpClient();
                m_blnUploadDone = false;
                ftp.DoneEvent += new FtpDLX.DoneDelegate(Upload_ftp_DoneEvent);

                ftp.PutFile(fi.FullName, target);

                DateTime timeout = DateTime.Now.AddSeconds(uploadTimeoutSeconds);

                while (!m_blnUploadDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Timeout occurred while uploading {0} to {1} with UseFtp set to true.", FormatPathForFTP(fi.FullName), URI));
                }

                if (target != targetFinalName)
                {
                    renameSucceeded = FtpRenameBetter(target, targetFinalName);
                }
            }
            catch (Exception e)
            {
                throw new IntegrationException(string.Format("Failed to upload {0} to {1}.\n\n{2}", FormatPathForFTP(fi.FullName), URI, e.ToString()));
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return renameSucceeded;
        }

        #endregion

        #region "Download: File transfer FROM ftp server"
        /// <summary>
        /// Copy a file from FTP server to local
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
            //1. check target
            if (targetFI.Exists && !(PermitOverwrite))
            {
                throw (new ApplicationException("Target file already exists"));
            }

            //2. check source
            string target;
            if (sourceFilename.Trim() == "")
            {
                throw (new ApplicationException("File not specified"));
            }
            else if (sourceFilename.Contains("/"))
            {
                //treat as a full path
                target = AdjustDir(sourceFilename);
            }
            else
            {
                //treat as filename only, use current directory
                target = CurrentDirectory + sourceFilename;
            }

            FtpDLX ftp = null;

            try
            {
                target = FormatPathForFTP(target);

                ftp = GetWODFtpClient();
                m_blnDownloadDone = false;
                ftp.DoneEvent += new FtpDLX.DoneDelegate(Download_ftp_DoneEvent);
                ftp.GetFile(targetFI.FullName, target);

                // Get timeout from config
                int uploadTimeoutSeconds = GetUploadTimeOut();

                DateTime timeout = DateTime.Now.AddSeconds(uploadTimeoutSeconds);

                while (!m_blnDownloadDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Timeout occurred while downloading {0} to {1}.\n\n", FormatPathForFTP(targetFI.FullName), target));
                }
            }
            catch
            {
                //delete target file as it's incomplete
                targetFI.Delete();
                throw;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
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
            FtpDLX ftp = null;

            try
            {
                string target = GetFullPath(filename);
                target = FormatPathForFTP(target);

                // 2012.06.26 JCopus - if this is the ShipRight FTP server then we have to strip of the leading slash
                // in order to make the delete work!  For some unknown reason, and only on the file delete, download works
                // fine, we have to use an unrooted path in order to delete the file.
                // For example:
                //    “/Daily Serial Number Report/Serial Number Report for 06-25-12 120000.csv”  - fails
                //    “Daily Serial Number Report/Serial Number Report for 06-25-12 120000.csv”   - works
                // The alternate to this would have been to write code to ChangeDirectory to the target folder and
                // then delete the file as a simply filespec.  But I just didn't want to do that for everyone since
                // SHipRight is the only one exhibiting this problem so far. If anyone care we were able to 
                // reproduce this failing behavior in windows command line FTP.
                if (this.isShipRightFtp)
                    target = target.Substring(1);

                ftp = GetWODFtpClient();
                m_blnFtpDeleteDone = false;
                ftp.DoneEvent += new FtpDLX.DoneDelegate(FtpDelete_ftp_DoneEvent);
                ftp.DeleteFile(target);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnFtpDeleteDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        break;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// Determine if file exists on remote FTP site
        /// </summary>
        /// <param name="filename">Filename (for current dir) or full path</param>
        /// <returns></returns>
        /// <remarks>Note this only works for files</remarks>
        public bool FtpFileExists(string filename)
        {
            List<string> remoteDir = ListDirectory(CurrentDirectory);
            bool fileExists = remoteDir.Contains(Path.GetFileName(filename));
            return fileExists;
        }

        /// <summary>
        /// Determine size of remote file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        /// <remarks>Throws an exception if file does not exist</remarks>
        public long GetFileSize(string filename)
        {
            string path;
            if (filename.Contains("/"))
                path = AdjustDir(filename);
            else
                path = this.CurrentDirectory + filename;

            filename = FormatPathForFTP(filename);
            long fileSize = 0;

            FtpDLX ftp = null;
            try
            {
                ftp = GetWODFtpClient();

                ftp.DoneEvent += GetFileSize_ftp_DoneEvent;
                m_blnGetFileSizeDone = false;
                m_lngGetFileSize_Size = 0;
                ftp.GetAttributes(filename);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnGetFileSizeDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Unable to get file attributes for file '{0}' on host '{1}'.", filename, ftp.Hostname));
                }

                fileSize = m_lngGetFileSize_Size;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return fileSize;
        }

        /// <summary>
        /// Rename a file
        /// </summary>
        /// <param name="sourceFilename">Source filename</param>
        /// <param name="newName">New filename</param>
        /// <returns>Boolean indicating outcome of operation</returns>
        public bool FtpRename(string sourceFilename, string newName)
        {
            //Does file exist?
            string source = GetFullPath(sourceFilename);
            if (!FtpFileExists(source))
            {
                throw (new FileNotFoundException("File " + source + " not found"));
            }

            //build target name, ensure it does not exist
            string target = GetFullPath(newName);
            if (target == source)
            {
                throw (new ApplicationException("Source and target are the same"));
            }
            else if (FtpFileExists(target))
            {
                throw (new ApplicationException("Target file " + target + " already exists"));
            }

            FtpDLX ftp = null;

            try
            {
                source = FormatPathForFTP(source);
                target = FormatPathForFTP(target);

                ftp = GetWODFtpClient();
                m_blnFtpRenameDone = false;
                ftp.DoneEvent += FtpRename_ftp_DoneEvent;
                ftp.Rename(target, source);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnFtpRenameDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(
                            string.Format(
                                "Warning: timeout occurred in FtpRename with sourceFilename='{0}', newName={1}",
                                sourceFilename, newName));
                }

                if (ftp.LastError != null)
                    throw ftp.LastError;
            }
            catch (Exception ex)
            {
                Diagnostics.TraceWarning(string.Format("Warning: exception thrown in FtpRename with sourceFilename='{0}', newName={1}:\r\n{2}\r\nStack trace:\r\n{3}", sourceFilename, newName, ex.Message, ex.StackTrace));
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// Renames a file
        /// </summary>
        /// <param name="sourceFilename"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public bool FtpRenameBetter(string sourceFilename, string newName)
        {
            //With one connection we check source file exists and target doesn't exist
            string source = GetFullPath(sourceFilename);
            string target = GetFullPath(newName);

            List<string> remoteDir = ListDirectory(FormatPathForFTP(CurrentDirectory));

            if (!remoteDir.Contains(Path.GetFileName(source)))
                throw (new FileNotFoundException("File " + source + " not found"));

            if (target == source)
            {
                throw (new ApplicationException("Source and target are the same"));
            }

            if (remoteDir.Contains(Path.GetFileName(target)))
            {
                throw (new ApplicationException("Target file " + target + " already exists"));
            }

            FtpDLX ftp = null;

            try
            {
                ftp = GetWODFtpClient();

                source = FormatPathForFTP(source);
                target = FormatPathForFTP(target);

                m_blnFtpRenameDone = false;
                ftp.DoneEvent += FtpRename_ftp_DoneEvent;
                ftp.Rename(target, source);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnFtpRenameDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        throw new IntegrationException(string.Format("Warning: timeout occurred in FtpRenameBetter with sourceFilename='{0}', newName={1}", sourceFilename, newName));
                }

                if (ftp.LastError != null)
                    throw ftp.LastError;
            }
            catch (Exception ex)
            {
                Diagnostics.TraceWarning(string.Format("Warning: exception thrown in FtpRenameBetter with sourceFilename='{0}', newName={1}:\r\n{2}\r\nStack trace:\r\n{3}", sourceFilename, newName, ex.Message, ex.StackTrace));
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// Creates FTP directory
        /// </summary>
        /// <param name="dirpath"></param>
        /// <returns></returns>
        public bool FtpCreateDirectory(string dirpath)
        {
            FtpDLX ftp = null;
            try
            {
                dirpath = FormatPathForFTP(dirpath);

                ftp = GetWODFtpClient();
                m_blnFtpCreateDirectoryDone = false;
                ftp.DoneEvent += FtpCreateDirectory_ftp_DoneEvent;
                ftp.MakeDir(dirpath);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnFtpCreateDirectoryDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        break;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// Deletes FTP directory
        /// </summary>
        /// <param name="dirpath"></param>
        /// <returns></returns>
        public bool FtpDeleteDirectory(string dirpath)
        {
            FtpDLX ftp = null;

            try
            {
                dirpath = FormatPathForFTP(dirpath);

                ftp = GetWODFtpClient();
                m_blnFtpDeleteDirectoryDone = false;

                ftp.DoneEvent += FtpDeleteDirectory_ftp_DoneEvent;
                ftp.RemoveDir(dirpath);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnFtpDeleteDirectoryDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        break;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool ClearCurrentDirectoryAlt()
        {
            FtpDLX ftp = null;

            try
            {
                ftp = GetWODFtpClient();

                List<FtpFileInfo> ftpFilesInfo = ListDirectoryDetail(CurrentDirectory);

                foreach (FtpFileInfo ftpFileInfo in ftpFilesInfo)
                    FtpDelete(ftpFileInfo.FullName);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// Deletes the file in the current directory
        /// </summary>
        /// <returns></returns>
        public bool ClearCurrentDirectory()
        {
            FtpDLX ftp = null;

            try
            {
                ftp = GetWODFtpClient();
                m_blnClearCurrentDirectoryDone = false;

                ftp.DoneEvent += ClearCurrentDirectory_ftp_DoneEvent;
                //ftp.ListItemsEvent += new FtpDLX.ListItemsDelegate(ClearCurrentDirectory_ftp_ListItemsEvent);
                ftp.DeleteFiles(CurrentDirectory, 1);

                DateTime timeout = DateTime.Now.AddSeconds(m_intOperationTimeOutInSeconds);

                while (!m_blnClearCurrentDirectoryDone)
                {
                    Thread.Sleep(100);

                    if (DateTime.Now >= timeout)
                        break;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (ftp != null)
                {
                    if (ftp.State != States.Disconnected) ftp.Disconnect();
                    ftp.Dispose();
                }
            }

            return true;
        }

        #endregion

        #region "private supporting fns"

        private string FormatPathForFTP(string strPath)
        {
            strPath = strPath.Replace("\\", "/");
            if (strPath.StartsWith("//"))
                strPath = strPath.Remove(1, 1);
            return strPath;
        }

        /// <summary>
        /// Parse address into hostname, path, and filename
        /// </summary>
        /// <param name="strAddress">Input address string</param>
        /// <returns>hostname in the format: ftp.host.com</returns>
        private void ParseAddress(string strAddress)
        {
            UriBuilder uriBuilder = new UriBuilder(strAddress);

            if (!string.IsNullOrEmpty(uriBuilder.Scheme) && !string.IsNullOrEmpty(uriBuilder.Host) && !string.IsNullOrEmpty(uriBuilder.Path))
            {
                _address = uriBuilder.Uri.AbsoluteUri;
                _hostname = uriBuilder.Host;
                _protocol = uriBuilder.Scheme;
                _currentDirectory = uriBuilder.Path;
                _port = uriBuilder.Port;

                if (_currentDirectory.IndexOf(".") > 0)
                    _currentDirectory = _currentDirectory.Remove(_currentDirectory.LastIndexOf("/") + 1);
            }
            else
                throw new IntegrationException(string.Format("Please check the configured ADDRESS in the FTP_ENDPOINT table {0}", Address));
        }

        /// <summary>
        /// Retrieves the certificate from Certificate store by thumbprint value
        /// </summary>
        /// <param name="thumbprintValue"></param>
        /// <returns></returns>
        private static X509Certificate2 FindCertificateByThumbprint(string thumbprintValue)
        {
            try
            {
                if (string.IsNullOrEmpty(thumbprintValue))
                    throw new IntegrationException(
                        string.Format("Thumbprint value cannot be null. Please check the Endpoint table SETTINGS."));
                var certificateStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                certificateStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var certificateCollection = certificateStore.Certificates.Find(X509FindType.FindByThumbprint,
                                                                               thumbprintValue, false);
                certificateStore.Close();
                return certificateCollection[0];
            }
            catch (Exception ex)
            {
                throw new IntegrationException(string.Format("Error while trying to read certificate from Store: Personal, StoreLocation : LocalMachine, Thumbprint : {0}. \r\n Exception : {1}", thumbprintValue, ex.ToString()));
            }
        }

        /// <summary>
        /// Returns FTP client instance
        /// </summary>
        /// <returns></returns>
        private FtpDLX GetWODFtpClient()
        {
            const string wodFTPGroupName = "wodFTP.NET";

            const string retryCountKeyName = "ConnectRetryCount";
            string retryCountCacheName = string.Format("{0}_{1}", wodFTPGroupName, retryCountKeyName);
            const string connectTimeOutKeyName = "ConnectTimeOutSeconds";
            string connectTimeOutCacheName = string.Format("{0}_{1}", wodFTPGroupName, connectTimeOutKeyName);
            const string operationTimeOutKeyName = "OperationTimeOutSeconds";
            string operationTimeOutCacheName = string.Format("{0}_{1}", wodFTPGroupName, operationTimeOutKeyName);

            if (m_intConnectRetryCount == 0)
            {
                lock (locker)
                {
                    if (!Cache.ContainsKey(retryCountCacheName))
                        Cache.Add(retryCountCacheName, Configuration.GetGeneralConfigValue(wodFTPGroupName, retryCountKeyName));

                    if (!Cache.ContainsKey(connectTimeOutCacheName))
                        Cache.Add(connectTimeOutCacheName, Configuration.GetGeneralConfigValue(wodFTPGroupName, connectTimeOutKeyName));

                    if (!Cache.ContainsKey(operationTimeOutCacheName))
                        Cache.Add(operationTimeOutCacheName, Configuration.GetGeneralConfigValue(wodFTPGroupName, operationTimeOutKeyName));
                }

                m_intConnectRetryCount = Int32.Parse(Cache.Get(retryCountCacheName).ToString());
                m_intConnectTimeOutInSeconds = Int32.Parse(Cache.Get(connectTimeOutCacheName).ToString());
                m_intOperationTimeOutInSeconds = Int32.Parse(Cache.Get(operationTimeOutCacheName).ToString());
            }

            if (m_intConnectRetryCount == 0)
                throw new Exception("Unable to determine wodFTP.NET connect retry count.");

            if (m_intConnectTimeOutInSeconds == 0)
                throw new Exception("Unable to determine wodFTP.NET connection timeout.");

            if (m_intOperationTimeOutInSeconds == 0)
                throw new Exception("Unable to determine wodFTP.NET operation timeout.");

            FtpDLX ftp = AttemptFTPConnect();

            for (int i = 0; i < m_intConnectRetryCount; i++)
            {
                if (ftp.State != States.Connected)
                {
                    ftp.Disconnect();
                    ftp.Dispose();
                    ftp = AttemptFTPConnect();
                }
                else
                {
                    break;
                }
            }

            if (ftp.State != States.Connected)
            {
                ftp.Disconnect();
                ftp.Dispose();
                throw new Exception(string.Format("Unable to connect to FTP host '{0}' after {1} retries.", _hostname, m_intConnectRetryCount.ToString()));
            }

            return ftp;
        }

        /// <summary>
        /// Attempts to connect to FTP server. It waits till the configured time to timout.
        /// </summary>
        /// <returns></returns>
        private FtpDLX AttemptFTPConnect()
        {
            FtpDLX ftp = GetAppropriateInstance();

            try
            {
                m_ftpReplyText = string.Empty;
                ftp.FtpReplyEvent += ftp_FtpReplyEvent;

                ftp.Connect();

                DateTime timeout = DateTime.Now.AddSeconds(m_intConnectTimeOutInSeconds);

                while (ftp.State != States.Connected)
                {
                    Thread.Sleep(100); // sleep for 10th of second

                    if (DateTime.Now >= timeout)
                        break;
                }
            }
            catch (Exception ex)
            {
                Diagnostics.TraceError("Error occurred during AttemptWODftpConnect: {0}", ex.Message);
            }

            if (ftp.State != States.Connected)
            {
                string error =
                    string.Format(
                        "Error occured while trying to connect to FTP server: '{0}', with User: '{1}, Password: '{2}'",
                        ftp.Hostname, ftp.Login, ftp.Password);
                if (m_ftpReplyText.Length > 0) error += "\r\n" + m_ftpReplyText;
                Diagnostics.TraceError(error);
            }

            return ftp;
        }

        /// <summary>
        /// Returns appropriate instance depending upon the Protocols (FTP, FTPS, SFTP)
        /// </summary>
        /// <returns></returns>
        private FtpDLX GetAppropriateInstance()
        {
            FtpDLX ftpInstance = new FtpDLX();
            ftpInstance.LicenseKey = "PEBW-NDDX-P3HP-A2ML";
            ftpInstance.Hostname = GetIPv4AddressByHostName(_hostname);
            ftpInstance.Login = string.IsNullOrEmpty(_username) ? "anonymous" : _username;
            ftpInstance.Password = string.IsNullOrEmpty(_password) ? "anonymous" : _password;

            switch (Protocol.ToUpper())
            {
                case "FTP":
                    ftpInstance.Authentication = Authentications.Password;
                    ftpInstance.Protocol = Protocols.FTP;
                    ftpInstance.Port = Port < 0 ? 21 : Port;
                    break;

                case "FTPS":
                    X509Certificate2 certificate = FindCertificateByThumbprint(XmlUtilities.SafeSelectText(EndpointSettings, "//CertificateThumbPrintValue"));

                    ftpInstance.Certificate = certificate;

                    if (certificate.HasPrivateKey)
                        ftpInstance.PrivateKey = certificate.PrivateKey;

                    if (bool.Parse(XmlUtilities.SafeSelectText(EndpointSettings, "//IsImplicit")))
                    {
                        ftpInstance.Protocol = Protocols.FTPSimplicit;
                        ftpInstance.Port = Port < 0 ? 990 : Port;
                    }
                    else
                    {
                        ftpInstance.Protocol = Protocols.FTPSwithdata;
                        ftpInstance.Port = Port < 0 ? 21 : Port;
                    }

                    ftpInstance.Authentication = Authentications.Both;
                    break;

                case "SFTP":
                    ftpInstance.Authentication = Authentications.Password;
                    ftpInstance.Protocol = Protocols.SFTP;
                    ftpInstance.Port = Port < 0 ? 22 : Port;
                    break;

                default:

                    throw new IntegrationException(string.Format("Invalid Protocol. Please check the Configured Address in the FTP Endpoint table"));
            }

            return ftpInstance;
        }

        /// <summary>
        /// Returns IPv4 Address
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        private string GetIPv4AddressByHostName(string host)
        {
            string strIPAddress = string.Empty;

            if (host.ToLower().Equals("localhost") || host.Equals("127.0.0.1"))
                host = Dns.GetHostName();

            IPAddress[] ipAddressList = Dns.GetHostAddresses(host);
            IPAddress address;
            foreach (IPAddress ipAddress in ipAddressList)
            {
                if (IPAddress.TryParse(ipAddress.ToString(), out address))
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                        strIPAddress = address.ToString();
                }
            }
            return strIPAddress;
        }

        /// <summary>
        /// returns a full path using CurrentDirectory for a relative file reference
        /// </summary>
        private string GetFullPath(string file)
        {
            if (file.Contains("/"))
            {
                return AdjustDir(file);
            }
            return this.CurrentDirectory + file;
        }

        /// <summary>
        /// returns the upload timeout in seconds
        /// </summary>
        private int GetUploadTimeOut()
        {
            // Get timeout from config
            int uploadTimeoutSeconds = 0;

            if (Cache.ContainsKey("WODFtp_UploadTimeoutSeconds"))
            {
                uploadTimeoutSeconds = Int32.Parse(Cache.Get("WODFtp_UploadTimeoutSeconds").ToString());
            }
            else
            {
                uploadTimeoutSeconds = Int32.Parse(Configuration.GetGeneralConfigValue("wodFTP.NET", "UploadTimeoutSeconds"));
                Cache.Add("WODFtp_UploadTimeoutSeconds", uploadTimeoutSeconds.ToString());
            }
            return uploadTimeoutSeconds;
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

        private string GetDirectory(string directory)
        {
            string URI;
            if (directory == "")
            {
                //build from current
                URI = Path.Combine(Hostname, this.CurrentDirectory);
                _lastDirectory = this.CurrentDirectory;
            }
            else
            {
                if (!directory.StartsWith("/"))
                {
                    throw (new ApplicationException("Directory should start with /"));
                }
                URI = this.Hostname + directory;
                _lastDirectory = directory;
            }
            return URI;
        }

        //stores last retrieved/set directory
        private string _lastDirectory = "";

        #endregion

        #region "Properties"

        private string _address;
        /// <summary>
        /// 
        /// </summary>
        public string Address
        {
            get { return _address; }
            set { _address = value; }
        }

        private string _protocol;
        /// <summary>
        /// 
        /// </summary>
        public string Protocol
        {
            get { return _protocol; }
            set { _protocol = value; }
        }

        private string _hostname;
        /// <summary>
        /// Hostname
        /// </summary>
        /// <value></value>
        /// <remarks>Hostname can be in either the full URL format
        /// ftp://ftp.myhost.com or just ftp.myhost.com
        /// </remarks>
        public string Hostname
        {
            get
            {
                return string.Format("{0}://{1}", Protocol, _hostname);
            }
            set
            {
                _hostname = value;
            }
        }

        private int _port;
        /// <summary>
        /// Port property
        /// </summary>
        /// <value></value>
        /// <remarks></remarks>
        public int Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
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
        /// <summary>
        /// 
        /// </summary>
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
                return _currentDirectory + ((_currentDirectory.EndsWith("/")) ? "" : "/").ToString();
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

        private XmlNode _endpointSettings;
        /// <summary>
        /// Sets and Returns Endpoint Settings in the Endpoint table
        /// </summary>
        public XmlNode EndpointSettings
        {
            get
            {
                return _endpointSettings;
            }
            set
            {
                _endpointSettings = value;
            }
        }
        #endregion
    }
}
