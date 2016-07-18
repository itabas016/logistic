using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using PayMedia.ApplicationServices.Devices.ServiceContracts;
using PayMedia.ApplicationServices.ScheduleManager.ServiceContracts;
using PayMedia.Integration.FrameworkService.Interfaces.Common;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    [Serializable]
    public class Logistic
    {
        #region Public Properties

        public IComponentInitContext context;

        public XmlNode WorkerSettings;

        public string errorRecord;

        public string errorFileName;

        public string buildListImportPath;

        public string ftpUrl;

        public string userName;

        public string pass;

        #endregion

        #region Ctor

        public Logistic(IComponentInitContext componentInitContext)
        {
            context = componentInitContext;
        }

        #endregion

        #region Public Method

        public virtual void Execute(FtpMessageInfo messageInfo)
        {

        }

        #endregion

        #region protected Methods

        protected virtual IDevicesService GetIBSDevicesService()
        {
            IDevicesService service = ServiceUtilities.GetService<IDevicesService>();
            return service;
        }

        protected virtual IDevicesConfigurationService GetIBSDevicesConfigurationService()
        {
            IDevicesConfigurationService service = ServiceUtilities.GetService<IDevicesConfigurationService>();
            return service;
        }

        protected virtual IScheduleManagerService GetScheduleManagerService()
        {
            IScheduleManagerService service = ServiceUtilities.GetService<IScheduleManagerService>();
            return service;
        }

        protected FtpClient GetFtpClient()
        {
            FtpClient ftp = new FtpClient();
            ftp.Hostname = ftpUrl;
            ftp.Username = userName;
            ftp.Password = pass;

            return ftp;
        }

        protected void FtpRenameFile(string fileName, string extension)
        {
            //string newFileName = fileName.Substring(0, fileName.LastIndexOf(".")) + extension;
            string newFileName = Path.ChangeExtension(fileName, extension);
            if (GetFtpClient().FtpRename(fileName, newFileName) == false)
            {
                string errorFilename = fileName ?? "<NULL fileName>";
                if (errorRecord == string.Empty)
                {
                    errorRecord = String.Format("EC_16 An unexpected error occurred while trying to rename the file '{0}' to '{1}' on the FTP server. Please check Warning level Event Log messages for details.\r\n", fileName, newFileName);
                }
                else
                {
                    errorRecord += string.Format("\r\nerror occurred while trying to rename the file '{0}' to '{1}' on the FTP server. Please check Warning level Event Log messages for details.\r\n", fileName, newFileName);
                }
                throw new Exception(errorRecord);
            }
        }

        protected void FtpLoadFile(string fileNamePath)
        {
            try
            {
                string fileName = System.IO.Path.GetFileName(fileNamePath);
                FileInfo fi = new FileInfo(fileNamePath);
                GetFtpClient().Upload(fi, fileName);
            }
            catch (Exception ex)
            {
                string errorFilename = fileNamePath ?? "<NULL fileName>";
                if (errorRecord == string.Empty)
                {
                    errorRecord = string.Format("EC_16 An unexpected error occurred. Loading the file: {0}. Error: {1}", errorFilename, ex.Message);
                }
                else
                {
                    errorRecord += string.Format("\r\nError loading the file: {0}. Error: {1}", errorFilename, ex.Message);
                }
                throw new Exception(errorRecord);
            }
        }

        protected void WriteToFile(string fileName, string record)
        {
            try
            {
                // lets make sure no two threads try to write to the file at the same time.
                lock (fileName)
                {
                    if (!File.Exists(fileName))
                    {
                        using (StreamWriter w = File.CreateText(fileName))
                        {
                            w.WriteLine("");
                            w.WriteLine(record);
                        }
                    }
                    else
                    {
                        using (StreamWriter w = File.AppendText(fileName))
                        {
                            w.WriteLine(record);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string errorFileName = fileName ?? "<NULL filename>";
                if (errorRecord == string.Empty)
                {
                    errorRecord = "EC_16f An unexpected error occurred while trying to write the file '" + errorFileName + "'.\r\n" + ex.ToString();
                }
                else
                {
                    errorRecord += string.Format("\r\nError writing to file: {0}", errorFileName);
                }
                throw new Exception(errorRecord);
            }

        }

        protected string SetFileExtension(string inputFilePath, string newExtensionName, bool replaceExtension, bool renameOnly)
        {
            string newInputFilePath = inputFilePath;

            if (replaceExtension)
            {
                newInputFilePath = Path.ChangeExtension(newInputFilePath, null);
            }

            newInputFilePath += newExtensionName;

            if (!renameOnly)
            {
                File.Copy(inputFilePath, newInputFilePath, true);
                File.Delete(inputFilePath);
            }

            return newInputFilePath;
        }

        #endregion
    }
}
