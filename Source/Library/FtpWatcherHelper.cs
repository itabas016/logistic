using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public class FtpWatcherHelper
    {
        public delegate void ProcessFileReceived(FtpMessageInfo messageInfo);
        public event ProcessFileReceived OnFileReceived;

        #region Private Fields

        private Thread listenerThread;
        private bool continueProcessing;
        private FtpWatcherConfiguration configuration;
        private object runningLocker;
        private const int FtpOperationRetryCount = 3;
        private const int FtpRetrySleepSeconds = 3;
        private const int PostFailureSleepSeconds = 30;
        private AutoResetEvent wakeUpEvent = new AutoResetEvent(false);

        //Added - 
        //private string perfmonInstanceName;
        //private IBSPerformanceListenerInstance perfmonInstance;
        private long start;
        //private Manager manager;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpListener"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public FtpWatcherHelper()
        {
            //perfmonInstanceName = configuration.Name == string.Empty ? "FtpWatcher" : configuration.Name;
            //Init ftp configuration

            this.configuration = InitConfiguration();
            this.runningLocker = new object();
            //this.manager = (Manager)configuration.Manager;
        }

        public FtpWatcherConfiguration InitConfiguration()
        {
            /*
            FtpWatcherConfiguration watcher = new FtpWatcherConfiguration();

            // FTP values.
            watcher.Name = ValidationUtilities.ParseString(reader, "FTP_WATCHER_NAME");

            watcher.DeleteAfterDownloading = ValidationUtilities.ParseBool(reader, "DELETE_AFTER_DOWNLOADING");
            watcher.PollingFileExtensions = ValidationUtilities.Split(ValidationUtilities.ParseString(reader, "FILE_EXTENSION_LIST"), 1, true, ",");
            watcher.PollingEndpoint = new FtpEndpoint();
            watcher.PollingEndpoint.Name = watcher.Name;
            watcher.PollingEndpoint.Settings = ValidationUtilities.ParseString(reader, "FTP_ENDPOINT_SETTINGS", true);
            watcher.PollingEndpoint.Address = ValidationUtilities.ParseString(reader, "SOURCE_FTP_ADDRESS");
            watcher.PollingEndpoint.Username = ValidationUtilities.ParseString(reader, "SOURCE_FTP_USERNAME");
            watcher.PollingEndpoint.Password = ValidationUtilities.ParseString(reader, "SOURCE_FTP_PASSWORD");
            watcher.PollingEndpoint.TransferInterval = new TimeSpan(0, 0, 0, 0, ValidationUtilities.ParseInt(reader, "SOURCE_TRANSFER_INTERVAL_MS"));
            watcher.PollingEndpoint.InTransitFileExtension = ValidationUtilities.ParseString(reader, "RENAME_AFTER_DL_EXT", true);

            // Storage values.
            FilePathEndpoint filePath = new FilePathEndpoint();
            filePath.Path = ValidationUtilities.ParseString(reader, "STORAGE_PATH");
            filePath.SpecialFolderName = ValidationUtilities.ParseString(reader, "STORAGE_SPECIAL_FOLDER", true);
            watcher.StorageFilePath = filePath.AbsolutePath;

            // Forwarding values.
            watcher.ForwardingEndpoint = CreateEndpoint(ValidationUtilities.ParseInt(reader, "ENDPOINT_TYPE_ID"));
            watcher.ForwardingEndpoint.InitFromDataReader(reader);
            string dsn = ValidationUtilities.ParseString(reader, "CONTEXT_DSN");
            string conditionName = ValidationUtilities.ParseString(reader, "CONTEXT_CONDITION_NAME");
            string conditionValue = ValidationUtilities.ParseString(reader, "CONTEXT_CONDITION_VALUE");
            watcher.ForwardingMailMessage = new IntegrationMailMessage(0, conditionName, conditionValue, 0, dsn, string.Empty, null, string.Empty);
            */

            return new FtpWatcherConfiguration();
        }

        public void RequestStart()
        {   //  note: 2009.11.20 - JCopus - Just to be safe I used perfmonInstanceName here.  
            //  If you have time to verify that a FtpWatcherConfiguration will ALWAYS have a name 
            //  then please use that name here instead.
            lock (runningLocker)
            {
                // Handle the case when the thread has already been started.
                if (listenerThread != null)
                    throw new IntegrationException("Ftp Listener already started.");

                LogInfo("Starting up.");
                this.wakeUpEvent.Reset();
                continueProcessing = true;
                listenerThread = new Thread(new ThreadStart(StartListening));
                listenerThread.Name = "FTPWatcher thread for - " + configuration.Name;
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
        }

        public bool RequestStop(int secondsToWait)
        {
            bool successfullyStopped = true;

            LogInfo("Stopping by request.");
            continueProcessing = false;
            this.wakeUpEvent.Set();

            successfullyStopped = listenerThread.Join(secondsToWait);

            if (successfullyStopped == true)
                GC.SuppressFinalize(this);

            return successfullyStopped;
        }

        public void ForceStop()
        {
            LogInfo("Stopping by force.");
            continueProcessing = false;
            listenerThread.Abort();
        }

        #region Private methods

        /// <summary>
        /// Starts listening.
        /// Prerequisite: Event OnFileReceived must be set.
        /// </summary>
        private void StartListening()
        {
            string remoteFilePath;

            try
            {
                // Ensure the processor has been set.
                if (OnFileReceived == null)
                    throw new IntegrationException(string.Format("{0} HandleFileEvent not set.", typeof(FtpWatcherHelper).Name));

                // Instantiate the FTP client.
                FtpClient ftpClient
                    = new FtpClient
                    (
                        configuration.PollingEndpoint.Address,
                        configuration.PollingEndpoint.Username,
                        configuration.PollingEndpoint.Password,
                        configuration.PollingEndpoint.Settings
                    );

                while (continueProcessing)
                {
                    TimeSpan sleepTime = configuration.PollingEndpoint.TransferInterval;
                    start = Stopwatch.GetTimestamp();
                    try
                    {
                        foreach (string pollingExtension in configuration.PollingFileExtensions)
                        {
                            if (continueProcessing == false)
                                break;

                            // Check for remote files matching the criteria.
                            remoteFilePath = FtpClient.FormatPathForSFTP(Path.Combine(ftpClient.CurrentDirectory, pollingExtension));
                            List<FtpFileInfo> fileInfoList = ListRemoteFiles(ftpClient, remoteFilePath);

                            // Process any files found.
                            foreach (FtpFileInfo fileInfo in fileInfoList)
                            {
                                if (continueProcessing == false)
                                    break;

                                // Get the localFilePath where file will be stored.
                                string localFilePath = Path.Combine(FileUtilities.CreateDailyFolder(configuration.StorageFilePath), fileInfo.Filename);

                                remoteFilePath = FtpClient.FormatPathForSFTP(Path.Combine(ftpClient.CurrentDirectory, fileInfo.Filename));

                                // See if we should rename the remote file before downloading.
                                if (!string.IsNullOrEmpty(configuration.PollingEndpoint.InTransitFileExtension))
                                {
                                    string inTransitFilename = Path.ChangeExtension(remoteFilePath, configuration.PollingEndpoint.InTransitFileExtension);
                                    if (ftpClient.FtpRename(remoteFilePath, inTransitFilename) == false)
                                    {
                                        string error = String.Format("Error while trying to rename the file '{0}' to '{1}' on the FTP server. Please check previous Warning level log messages for details.\r\n", remoteFilePath, inTransitFilename);
                                        LogError(error);

                                        // Log the error and sleep for a while so as not to kill the FTP server.
                                        sleepTime = new TimeSpan(0, 0, PostFailureSleepSeconds);

                                        // even if we have an error with this file continue on and try to check any remaining
                                        // files.  This way one bad file doesn't prevent the processing of others.
                                        continue;
                                    }

                                    // update the remoteFilePath so that we download using the newly renamed file.
                                    remoteFilePath = inTransitFilename;
                                    fileInfo.Extension = configuration.PollingEndpoint.InTransitFileExtension;
                                }

                                //Download the remote file
                                DownloadRemoteFile(ftpClient, remoteFilePath, localFilePath);

                                //Delete the remote file depending upon the flag DELETE_AFTER_DOWNLOADING set in FTP_WATCHER table
                                if (configuration.DeleteAfterDownloading)
                                    DeleteRemoteFile(ftpClient, remoteFilePath, localFilePath);

                                // Raise the file-received event.
                                if (OnFileReceived != null)
                                {
                                    FtpMessageInfo messageInfo = new FtpMessageInfo(fileInfo, localFilePath, ftpClient.Address);
                                    OnFileReceived(messageInfo);
                                }
                            }// end foreach (FtpFileInfo)
                        }// end foreach (string extension ...)

                        //perfmonInstance.IncrementItemRecNAvgExec(start, Stopwatch.GetTimestamp());

                    }
                    catch (ThreadAbortException)
                    {
                        continueProcessing = false;
                        break;
                    }
                    catch (Exception e)
                    {
                        //perfmonInstance.IncrementError();
                        // Log the error and sleep for a while so as not to kill the FTP server.
                        sleepTime = new TimeSpan(0, 0, PostFailureSleepSeconds);
                        LogError(string.Format("{0}\r\n\r\nSleeping for time {1}.", e.ToString(), sleepTime.ToString()));
                    }

                    // After processing the complete set of extensions, sleep before polling again.
                    // (note: we use wakeUpEvent.WaitOne() to sleep so that the user can signal us to wake up and stop if needed)
                    if (continueProcessing)
                        this.wakeUpEvent.WaitOne(sleepTime);
                }// end while (continueProcessing)
            }
            catch (Exception ex)
            {
                string error = string.Format("Error occurred while trying to Start FTP Watcher.  Please check your Configuration\r\n{0}\r\n", ex.ToString());
                LogError(error);
                //manager.ScheduleShutdown(30);
            }
        }

        private List<FtpFileInfo> ListRemoteFiles(FtpClient ftpClient, string remotePath)
        {
            List<FtpFileInfo> fileInfo = null;

            bool success = false;
            for (int attempts = 0; attempts < FtpOperationRetryCount; attempts++)
            {
                // Attempt the operation.
                try
                {
                    fileInfo = ftpClient.ListFiles(remotePath);
                    success = true;
                    break;
                }
                catch (WebException)
                {
                    // Sleep a bit before trying again.
                    Thread.Sleep(new TimeSpan(0, 0, FtpRetrySleepSeconds));
                }
            }

            if (!success)
                throw new IntegrationException(string.Format("Failed {0} attempts to get directory listing of {1}.", FtpOperationRetryCount, remotePath));

            return fileInfo;
        }

        private void DownloadRemoteFile(FtpClient ftpClient, string remotePath, string localPath)
        {
            bool success = false;
            for (int attempts = 0; attempts < FtpOperationRetryCount; attempts++)
            {
                // Attempt the operation.
                string message;
                try
                {
                    // Added 5/9/08: Since name of file is no longer unique as it is downloaded, need to keep track of old file.
                    if (System.IO.File.Exists(localPath))
                    {
                        // This is the fastest way to rename a file.
                        System.IO.File.Move(localPath, Path.ChangeExtension(localPath, Guid.NewGuid().ToString()));
                        System.IO.File.Delete(localPath);
                    }

                    ftpClient.Download(remotePath, localPath, true);

                    if (System.IO.File.Exists(localPath))
                    {
                        success = true;
                        break;
                    }

                    message = "no result on disk";
                }
                catch (WebException e)
                {
                    message = e.Message;
                }

                // Log a failure warning and sleep a bit before trying again.
                LogWarning(string.Format("Attempt {0} of {1} to download file yielded {2}.\r\nRemote Source: '{3}'\r\nDestination: '{4}'\r\n", attempts + 1, FtpOperationRetryCount, message, ftpClient.Hostname + remotePath, localPath));
                Thread.Sleep(new TimeSpan(0, 0, FtpRetrySleepSeconds));
            }

            if (!success)
                throw new IntegrationException(string.Format("Failed {0} attempts to download file from '{1}' to '{2}'.\r\nPlease verify that the FTP URL is correct AND that you have enough permissions on that FTP site to Rename, Download and Upload files.\r\n", FtpOperationRetryCount, ftpClient.Hostname + remotePath, localPath));
        }

        private void DeleteRemoteFile(FtpClient ftpClient, string remotePath, string localPath)
        {
            bool success = false;
            for (int attempts = 0; attempts < FtpOperationRetryCount; attempts++)
            {
                // Attempt the operation.
                string message;
                try
                {
                    ftpClient.FtpDelete(remotePath);
                    if (!ftpClient.FtpFileExists(remotePath))
                    {
                        success = true;
                        break;
                    }

                    message = "no change on server";
                }
                catch (WebException e)
                {
                    message = e.Message;
                }

                // Log a failure warning and sleep a bit before trying again.
                LogWarning(string.Format("Attempt {0} of {1} to delete remote file yielded {2}.\r\nRemote Source: {3}", attempts + 1, FtpOperationRetryCount, message, remotePath));
                Thread.Sleep(new TimeSpan(0, 0, FtpRetrySleepSeconds));
            }

            if (!success)
            {
                // Delete the local file and its directory since the overall action failed.
                System.IO.File.Delete(Path.GetDirectoryName(localPath));

                throw new IntegrationException(string.Format("Failed {0} attempts to delete the remote file from {1}.\r\nSleeping for time {2}.", FtpOperationRetryCount, remotePath));
            }
        }

        private void LogError(string message)
        {
            Diagnostics.TraceError(string.Format("Error from FtpWatcher {0}.\r\n\r\n{1}\r\n\r\n{2}", configuration.PollingEndpoint.Name, message, configuration.PollingEndpoint.ToString()));
        }

        private void LogWarning(string message)
        {
            Diagnostics.TraceWarning(string.Format("Warning from FtpWatcher {0}.\r\n\r\n{1}\r\n\r\n{2}", configuration.PollingEndpoint.Name, message, configuration.PollingEndpoint.ToString()));
        }

        private void LogInfo(string message)
        {
            Diagnostics.TraceInformation(string.Format("Info from FtpWatcher {0}.\r\n\r\n{1}\r\n\r\n{2}", configuration.PollingEndpoint.Name, message, configuration.PollingEndpoint.ToString()));
        }

        #endregion

    }
}
