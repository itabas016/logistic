using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Entriq.DataAccess;
using PayMedia.ApplicationServices.Devices.ServiceContracts;
using PayMedia.ApplicationServices.Devices.ServiceContracts.DataContracts;
using PayMedia.ApplicationServices.ScheduleManager.ServiceContracts;
using PayMedia.ApplicationServices.ScheduleManager.ServiceContracts.DataContracts;
using PayMedia.ApplicationServices.SharedContracts;
using PayMedia.Integration.FrameworkService.Interfaces.Common;
using LookupLists = PayMedia.ApplicationServices.Devices.ServiceContracts.LookupLists;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    [Serializable]
    public class L_01_UploadDevicesAndPairing : Logistic
    {
        #region Properties

        [NonSerialized]
        [XmlIgnore]
        Dictionary<string, DeviceImportRecord> serialNumbers;

        [NonSerialized]
        [XmlIgnore]
        List<DeviceImportRecord> DevicesList;

        [NonSerialized]
        [XmlIgnore]
        List<DeviceImportRecord> NewDevices;

        [NonSerialized]
        [XmlIgnore]
        List<DeviceImportRecord> NewSmartCards;

        [NonSerialized]
        [XmlIgnore]
        List<DeviceImportRecord> UpdateDevices;

        [NonSerialized]
        [XmlIgnore]
        List<DeviceImportRecord> UpdateSmartCards;

        [NonSerialized]
        [XmlIgnore]
        List<DeviceImportRecord> PairedDevices;

        [NonSerialized]
        [XmlIgnore]
        Dictionary<LocationIDModelName, string> DeviceLocIdModels;

        [NonSerialized]
        [XmlIgnore]
        Dictionary<LocationIDModelName, string> SmartCardLocIdModels;

        [NonSerialized]
        [XmlIgnore]
        Dictionary<DeviceImportRecord, CustomFieldValueCollection> CustomFieldsToUpdate;

        [NonSerialized]
        [XmlIgnore]
        DeviceImportLogRecord logRecord;

        [NonSerialized]
        [XmlIgnore]
        string deviceTypePaired;

        [NonSerialized]
        [XmlIgnore]
        string externalStockHandlerID;

        [NonSerialized]
        [XmlIgnore]
        L_01_Utils l_01_utils;

        int maxNumberOfProcessingThreads = 5;
        volatile int currentNumberOfProcessingThreads = 0;
        int numberOfRecords = 0;
        int numberOfPairs = 0;
        int succeededRecords = 0;
        volatile int failedRecords = 0;
        string delimiterString = "|";

        #endregion

        #region Ctor

        public L_01_UploadDevicesAndPairing()
        {
            this.WorkerSettings = Configuration.WorkerSetting;
        }

        #endregion

        #region Public Methods

        public override void Execute(FtpMessageInfo messageInfo)
        {
            //Get configuration
            GetConfigValues(WorkerSettings);

            //initialize all collections
            InitializeLocalCollections();

            // Process the file.
            string InputFilePath = messageInfo.FilePath;

            // Archive the file and rename the extension
            string originalFileName = Path.GetFileName(InputFilePath);
            string ftpFileName = SetFileExtension(originalFileName, ".process", true, true);
            string archivePath = XmlUtilities.SafeSelect(WorkerSettings, "ArchivePath").InnerText;
            archivePath = Path.Combine(archivePath, originalFileName);
            errorFileName = InputFilePath;
            errorFileName = SetFileExtension(errorFileName, ".error", true, true);

            if (File.Exists(errorFileName))
            {
                File.Delete(errorFileName);
            }

            try
            {
                File.Copy(InputFilePath, archivePath, true);
            }
            catch (Exception copyException)
            {
                try
                {
                    FtpRenameFile(ftpFileName, ".fail");
                }
                catch (Exception ex)
                {
                    throw new IntegrationException(ex.Message, ex);
                }
                throw new IntegrationException(copyException.Message, copyException);
            }

            char[] delimiterChars = XmlUtilities.SafeSelect(WorkerSettings, "StringDelimiter").InnerText.ToCharArray();

            int expectedRecords = 0;
            int expectedPairs = 0;

            //validate the file
            try
            {
                errorRecord = string.Empty;

                // *** Detect byte order mark; check if UTF-8 if used
                byte[] buffer = new byte[5];
                FileStream file = new FileStream(InputFilePath, FileMode.Open);
                file.Read(buffer, 0, 5);
                file.Close();
                if (buffer[0] != 0xef || buffer[1] != 0xbb || buffer[2] != 0xbf)
                {
                    throw new Exception("Input file MUST be created using UTF-8 encoding");
                }

                // Create an instance of StreamReader to read from a file.
                // The using statement also closes the StreamReader.                    
                using (StreamReader sr = new StreamReader(InputFilePath, Encoding.UTF8))
                {
                    string line;
                    line = sr.ReadLine();
                    string[] lineRecord = line.Split(delimiterChars);

                    if (lineRecord.Length == 2)
                    {
                        //get the numbers of records and pairs
                        if (int.TryParse(lineRecord[0], out expectedRecords) == false)
                        {
                            string error = string.Format("Error: The Header Record value for Expected Number of Records is NOT an integer value.  The value read was '{0}'.  Please correct your data file.\r\n", lineRecord[0]);
                            throw new IntegrationException(error);
                        }

                        if (int.TryParse(lineRecord[1], out expectedPairs) == false)
                        {
                            string error = string.Format("Error: The Header Record value for Expected Number of Pairings is NOT an integer value.  The value read was '{0}'.  Please correct your data file.\r\n", lineRecord[1]);
                            throw new IntegrationException(error);
                        }
                    }
                    else
                    {
                        string error = string.Format("Error: The Header Record is Should contain Exactly two fields. The Header Record found was '{0}'.  Please correct your data file.\r\n", line);
                        throw new IntegrationException(error);
                    }

                    // Read and validate the records.
                    while ((line = sr.ReadLine()) != null)
                    {
                        numberOfRecords++;
                        ValidateFileStructure(line, delimiterChars, numberOfRecords);
                    }

                    if (expectedRecords != numberOfRecords)
                    {
                        errorRecord = string.Format("EC_2 Headerfield NumberOfRecords does not match the number of records in the file. Expected: {0} actual: {1}. ",
                            expectedRecords, numberOfRecords);
                        throw new Exception(errorRecord);
                    }

                    if (expectedPairs != numberOfPairs)
                    {
                        errorRecord = string.Format("EC_3 Headerfield NumberOfPairs{0} does not match the number of pairing records in the file {1}. ",
                            expectedPairs, numberOfPairs);
                        throw new Exception(errorRecord);
                    }
                }

                CreateDeviceImportLogRecord(InputFilePath, archivePath);
            }
            catch (Exception validationException)
            {
                // Let the user know what went wrong.
                if (errorRecord == string.Empty)
                {
                    errorRecord = "EC_16a An unexpected error occurred. " + validationException.Message;
                }
                else
                {
                    errorRecord = validationException.Message;
                }
                try
                {
                    WriteToFile(errorFileName, errorRecord);
                    // rename the original file to fail
                    FtpRenameFile(ftpFileName, ".fail");
                    // load the error file
                    FtpLoadFile(errorFileName);
                }
                catch (Exception ex)
                {
                    throw new IntegrationException(errorRecord, ex);
                }
                throw new IntegrationException(errorRecord, validationException);
            }

            //process the file
            try
            {
                ProcessFile();
                if (failedRecords > 0)
                {
                    // rename the original file to fail
                    FtpRenameFile(ftpFileName, ".partial");
                    // load the error file
                    FtpLoadFile(errorFileName);
                }
                else
                {
                    // rename the original file to fail
                    FtpRenameFile(ftpFileName, ".success");
                }
                UpdateDeviceImportLogRecord(succeededRecords, failedRecords);
            }
            catch (Exception processException)
            {
                // Let the user know what went wrong.
                if (errorRecord == string.Empty)
                {
                    errorRecord = "EC_16b An unexpected error occurred. " + processException.Message;
                }
                else
                {
                    errorRecord = processException.Message;
                }

                try
                {
                    WriteToFile(errorFileName, errorRecord);
                    // rename the original file to fail
                    FtpRenameFile(ftpFileName, ".fail");
                    // load the error file
                    FtpLoadFile(errorFileName);
                }
                catch (Exception ex)
                {
                    //TODO: if this exception is raised we probably want to write some error using IcLogger stating the failure of the above file / FTP failure before re-throwing our exception. - JCopus
                    throw new IntegrationException(errorRecord, ex);
                }
                throw new IntegrationException(errorRecord, processException);
            }
        }

        #endregion

        #region protected Methods

        protected void LoadDictionaries()
        {
            string defaultSmartcardModelName = XmlUtilities.SafeSelect(WorkerSettings, "DefaultSmartCardModelName").InnerText;

            this.currentNumberOfProcessingThreads = 0;
            using (AutoResetEvent doneProcessing = new AutoResetEvent(false))
            {
                foreach (DeviceImportRecord deviceImportRecord in DevicesList)
                {
                    while (currentNumberOfProcessingThreads >= maxNumberOfProcessingThreads)
                        doneProcessing.WaitOne(1000);

                    Interlocked.Increment(ref this.currentNumberOfProcessingThreads);

                    DeviceImportRecordThreadParam myParams = new DeviceImportRecordThreadParam();
                    myParams.deviceImportRecord = deviceImportRecord;
                    myParams.doneProcessing = doneProcessing;

                    Thread thread = new Thread(new ParameterizedThreadStart(LoadDeviceIntoDictionaries));
                    thread.IsBackground = true;
                    thread.Name = string.Format("LoadDeviceIntoDictionaries - {0}", currentNumberOfProcessingThreads);
                    thread.Start(myParams);
                }

                // wait for our worker threads to finish.
                DateTime waitForThreadsToFinishUntil = DateTime.Now.AddMinutes(5);
                while (this.currentNumberOfProcessingThreads > 0 && DateTime.Now < waitForThreadsToFinishUntil)
                    doneProcessing.WaitOne(1000);
            }
            if (this.currentNumberOfProcessingThreads > 0)
                throw new IntegrationException(string.Format("Error: Timeout occured while waiting for {0} of our process threads to finish loading our dictionaries.", currentNumberOfProcessingThreads));

        }

        protected void ProcessFile()
        {
            LoadDictionaries();

            #region Process NewDevices

            if (NewDevices != null && NewDevices.Count > 0)
            {
                foreach (DeviceImportRecord deviceImportRecord in NewDevices)
                {
                    AddRecordToDevicesFile(deviceImportRecord);
                }
            }

            if (DeviceLocIdModels != null)
            {
                foreach (KeyValuePair<LocationIDModelName, string> kvp in DeviceLocIdModels)
                {
                    ProcessNewDevices(kvp.Key.ModelName, kvp.Key.LocationID, kvp.Key.DeviceStatusCode, kvp.Value);
                }
            }

            #endregion

            #region Process NewSmartCards

            if (NewSmartCards != null && NewSmartCards.Count > 0)
            {
                foreach (DeviceImportRecord deviceImportRecord in NewSmartCards)
                {
                    AddRecordToSmartCardsFile(deviceImportRecord);
                }
            }

            if (SmartCardLocIdModels != null)
            {
                foreach (KeyValuePair<LocationIDModelName, string> kvp in SmartCardLocIdModels)
                {
                    ProcessNewDevices(kvp.Key.ModelName, kvp.Key.LocationID, kvp.Key.DeviceStatusCode, kvp.Value);
                }
            }

            #endregion

            #region Process UpdateDevices

            if (UpdateDevices != null && UpdateDevices.Count > 0)
            {
                foreach (DeviceImportRecord deviceImportRecord in UpdateDevices)
                {
                    ProcessUpdateDevices(deviceImportRecord, DeviceType.SetupBox);
                }
            }

            #endregion

            #region Process UpdateSmartCards

            if (UpdateSmartCards != null && UpdateSmartCards.Count > 0)
            {
                foreach (DeviceImportRecord deviceImportRecord in UpdateSmartCards)
                {
                    ProcessUpdateDevices(deviceImportRecord, DeviceType.SmartCard);
                }
            }

            #endregion

            #region Process PairedDevices

            if (PairedDevices != null)
            {
                // since these paired devices do not depend on each other then lets process them 
                // concurrently up to this.maxNumberOfProcessingThreads.
                this.currentNumberOfProcessingThreads = 0;
                using (AutoResetEvent doneProcessing = new AutoResetEvent(false))
                {
                    foreach (DeviceImportRecord deviceImportRecord in PairedDevices)
                    {
                        while (this.currentNumberOfProcessingThreads >= this.maxNumberOfProcessingThreads)
                            doneProcessing.WaitOne(1000);

                        Interlocked.Increment(ref this.currentNumberOfProcessingThreads);

                        DeviceImportRecordThreadParam deviceImportRecordThreadParam = new DeviceImportRecordThreadParam();
                        deviceImportRecordThreadParam.deviceImportRecord = deviceImportRecord;
                        deviceImportRecordThreadParam.doneProcessing = doneProcessing;

                        Thread thread = new Thread(new ParameterizedThreadStart(this.ProcessPairedDevices));
                        thread.IsBackground = true;
                        thread.Name = string.Format("ProcessPairedDevices - {0}", currentNumberOfProcessingThreads);
                        thread.Start(deviceImportRecordThreadParam);
                    }

                    // wait for our worker threads to finish.
                    DateTime waitForThreadsToFinishUntil = DateTime.Now.AddMinutes(5);
                    while (this.currentNumberOfProcessingThreads > 0 && DateTime.Now < waitForThreadsToFinishUntil)
                        doneProcessing.WaitOne(1000);
                }
                if (this.currentNumberOfProcessingThreads > 0)
                    throw new IntegrationException(string.Format("Error: Timeout occured while waiting for {0} of our process threads to finish Processing Paired Devices.", currentNumberOfProcessingThreads));
            }

            #endregion

            UpdateCustomFields();
        }

        protected void CreateDeviceImportLogRecord(string inputFilePath, string archivePath)
        {
            InitializeLogRecord();
            errorRecord = string.Empty;
            string originalFileName = Path.GetFileName(inputFilePath);
            int runId = 0;

            try
            {
                char[] delimiterChars = { '.' };
                string[] fileName = inputFilePath.Split(delimiterChars);
                logRecord.RunID = Convert.ToInt32(fileName[2].ToString());
                logRecord.ID = 0;
                logRecord.SourceURI = ftpUrl + "/" + originalFileName;
                logRecord.ArchivePath = archivePath;
                if (fileName.Length > 4)
                {
                    int.TryParse(fileName[3].ToString(), out logRecord.FixedRunID);
                }

                int resultCount;

                using (IDataAccess da = DataAccessFactory.CreateDataAccess(Configuration.AppSettings.IntegrationDataDsn))
                {
                    runId = Convert.ToInt32(da.ExecuteScalar("select NVL (MAX (RUN_ID), 0) + 1 from CI_DEVICE_IMPORT_LOG "));
                    if (runId != logRecord.RunID)
                    {
                        errorRecord = string.Format("EC_16 An unexpected error occurred. RunId {0} not a contiguous value; next run id expected is {1}"
                            , logRecord.RunID, runId);
                        throw new Exception();
                    }

                    resultCount = Convert.ToInt32(da.ExecuteScalar(string.Format("select count(*) from CI_DEVICE_IMPORT_LOG " +
                        " where RUN_ID = " + logRecord.RunID)));
                    if (resultCount == 0 && logRecord.FixedRunID != 0)
                    {
                        errorRecord = string.Format("EC_16 An unexpected error occurred. File name contains FixedRunId {0} " +
                            "for new RunId {1}", logRecord.FixedRunID, logRecord.RunID);
                    }
                    else if (resultCount != 0 && logRecord.FixedRunID == 0)
                    {
                        errorRecord = "EC_1 Duplicate RunID received. RunId: " + logRecord.RunID.ToString();
                    }
                    else
                    {
                        string nonQuerySql = "";
                        if (resultCount == 0)
                        {
                            //TODO: Turn this in to parameterized SQL.
                            nonQuerySql = "Insert into  CI_DEVICE_IMPORT_LOG ( ID, RUN_ID, SOURCE_URI, ARCHIVE_PATH, START_TIME) " +
                                " values ((SELECT NVL(MAX(ID), 0) + 1 from CI_DEVICE_IMPORT_LOG), " + logRecord.RunID +
                                ", '" + logRecord.SourceURI + "', '" + logRecord.ArchivePath +
                                "', to_date('" + DateTime.Now.ToString("M/d/yyyy HH:mm:ss") + "', 'mm/dd/yyyy hh24:mi:ss'))";
                        }
                        else
                        {
                            //TODO: Turn this in to parameterized SQL.
                            nonQuerySql = "Update CI_DEVICE_IMPORT_LOG set START_TIME = to_date('" +
                                DateTime.Now.ToString("M/d/yyyy HH:mm:ss") + "', 'mm/dd/yyyy hh24:mi:ss')" +
                                " Where ID = " + logRecord.RunID.ToString();
                        }
                        da.ExecuteNonQuery(nonQuerySql);
                    }
                }
                if (errorRecord != string.Empty)
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                if (errorRecord == string.Empty)
                {
                    errorRecord = string.Format("EC_16 An unexpected error occurred in CreateDeviceImportLogRecord. Error: {0}", ex.Message);
                }
                throw new Exception(errorRecord);
            }
        }

        protected void UpdateDeviceImportLogRecord(int successRecords, int failRecords)
        {
            try
            {
                using (IDataAccess da = DataAccessFactory.CreateDataAccess(Configuration.AppSettings.IntegrationDataDsn))
                {
                    //TODO: Turn this in to parameterized SQL.
                    string updateSql = "Update CI_DEVICE_IMPORT_LOG " +
                        "set END_TIME = to_date('" + DateTime.Now.ToString("M/d/yyyy HH:mm:ss") + "', 'mm/dd/yyyy hh24:mi:ss')" +
                        ", SUCCEEDED_COUNT = " + successRecords.ToString() +
                        ", FAILED_COUNT = " + failRecords.ToString() +
                        " where RUN_ID = " + logRecord.RunID.ToString();

                    da.ExecuteNonQuery(updateSql);
                }
            }
            catch (Exception ex)
            {
                if (errorRecord == string.Empty)
                {
                    errorRecord = string.Format("EC_16 An unexpected error occurred in UpdateDeviceImportLogRecord. Error: {0}", ex.Message);
                }
                throw new Exception(errorRecord);
            }
        }

        protected void ValidateFileStructure(string line, char[] delimiterChars, int recordNumber)
        {
            int pos;
            errorRecord = string.Empty;
            try
            {
                string[] record = line.Split(delimiterChars);

                // check for duplicate values in serial numbers
                if (serialNumbers.ContainsKey(record[0]))
                {
                    errorRecord = string.Format("EC_6 Duplicate serial number found in file {0}. ", record[0]);
                    throw new Exception(errorRecord);
                }

                DeviceImportRecord deviceImportRecord = new DeviceImportRecord();
                deviceImportRecord.recordNumber = recordNumber;
                deviceImportRecord.SerialNumber = record[0];
                deviceImportRecord.DeviceType = record[1];
                deviceImportRecord.ModelName = record[2].Trim();
                deviceImportRecord.StockHandlerType = record[3];
                deviceImportRecord.LocationID = record[4].Trim();
                deviceImportRecord.DeviceStatusCode = record[5];
                deviceImportRecord.ChipsetID = record[6];

                #region Check Model Name and Location ID

                // 2010.12.01 JCopus IBSO 19627 - the ModelName is used as part of a filename later on in processing.
                // If it contains invalid filename characters it will cause this issue to occur.  Se lets check for
                // invalid characters and throw an error if we find some.
                pos = deviceImportRecord.ModelName.IndexOfAny(Path.GetInvalidFileNameChars());
                if (pos > 0)
                {
                    throw new Exception("EC_16g The 'Model Name', field 3, contains an invalid character");
                }

                // 2010.12.01 JCopus IBSO 19627 - the Location ID is used as part of a filename later on in processing.
                // If it contains invalid filename characters it will cause this issue to occur.  Se lets check for
                // invalid characters and throw an error if we find some.
                pos = deviceImportRecord.LocationID.IndexOfAny(Path.GetInvalidFileNameChars());
                if (pos > 0)
                {
                    throw new Exception("EC_16g The 'Location ID', field 5, contains an invalid character");
                }

                #endregion

                serialNumbers.Add(deviceImportRecord.SerialNumber, deviceImportRecord);

                #region record index 7 ==> Smart Card serial number

                if (record[7] != string.Empty) // Smart Card serial number
                {
                    if (serialNumbers.ContainsKey(record[7]))
                    {
                        errorRecord = string.Format("EC_6 Duplicate serial number found in file {0}. ", record[7]);
                        throw new Exception(errorRecord);
                    }
                    if (deviceImportRecord.DeviceType != deviceTypePaired)
                    {
                        errorRecord = string.Format("EC_5 Pairing record (0) does not have the correct DeviceType " + deviceTypePaired + ". ", recordNumber);
                        throw new Exception(errorRecord);
                    }
                    else if ((deviceImportRecord.DeviceType == deviceTypePaired) && (record[7] == string.Empty))
                    {
                        errorRecord = string.Format("EC_4 Pairing record (0) does not have content in the field SmartCardSerialNumberToPair.", recordNumber);
                        throw new Exception(errorRecord);
                    }

                    if (record.Length > 7)
                    {
                        deviceImportRecord.SmartCardSerialToPair = record[7];

                        // 2010.12.01 JCopus IBSO 19627 - Sometimes it seems that the BBCL file has extra tabs on the 
                        // end of some of the lines.  So we will strip all leading and trailing white space on this field now.
                        deviceImportRecord.SmartCardToPairModelName = record[8].Trim();
                        pos = deviceImportRecord.SmartCardToPairModelName.IndexOfAny(Path.GetInvalidFileNameChars());
                        if (pos > 0)
                        {
                            throw new Exception("EC_16g The 'Smart Card To Pair Model Name', field 9, contains an invalid character");
                        }
                    }

                    serialNumbers.Add(deviceImportRecord.SmartCardSerialToPair, deviceImportRecord);
                }

                #endregion

                if (deviceImportRecord.DeviceType == deviceTypePaired)
                {
                    numberOfPairs++;
                }

                #region record length > 9

                if (record.Length > 9)
                {
                    if (record.Length % 2 == 0)
                    {
                        errorRecord = string.Format("EC_13 An odd number of fields supplied at record number: {0}", recordNumber);
                        throw new Exception(errorRecord);
                    }
                    CustomFieldValueCollection cfvCollection = new CustomFieldValueCollection();
                    for (int j = 9; j < record.Length; j = j + 2)
                    {
                        int customFieldId = 0;
                        if (this.l_01_utils.DeviceCustomFieldExists(record[j], out customFieldId))
                        {
                            if (string.IsNullOrEmpty(record[j + 1]))
                            {
                                errorRecord = string.Format("EC_13 Value not provided for Custom Field {0} at record number: {1}.", record[j], recordNumber);
                                throw new Exception(errorRecord);
                            }
                            CustomFieldValue newItem = new CustomFieldValue();
                            newItem.Id = customFieldId;
                            newItem.Name = record[j];
                            newItem.Value = record[j + 1];
                            cfvCollection.Add(newItem);
                        }
                        else
                        {
                            errorRecord = string.Format("EC_13 Custom field name {0} not configured in IBS at record number: {1}.", record[j], recordNumber);
                            throw new Exception(errorRecord);
                        }
                        deviceImportRecord.CustomFields += "|" + record[j] + "|" + record[j + 1];
                    }// end for loop

                    if (cfvCollection.Count > 0)
                    {
                        CustomFieldsToUpdate.Add(deviceImportRecord, cfvCollection);
                    }
                }

                #endregion

                DevicesList.Add(deviceImportRecord);

                if (string.IsNullOrEmpty(deviceImportRecord.LocationID))
                {
                    errorRecord = string.Format("EC_8 LocationID is blank for record number: {0}.", recordNumber);
                    throw new Exception(errorRecord);
                }
            }
            catch (Exception ex)
            {
                if (errorRecord == string.Empty)
                {
                    errorRecord = string.Format("EC_16c An unexpected error occurred. {0}", ex.Message);
                }
                throw new Exception(errorRecord);
            }

        }

        protected void UpdateCustomFields()
        {
            if (this.CustomFieldsToUpdate != null)
            {
                // since these custom fields do not depend on each other then lets process them 
                // concurrently up to this.maxNumberOfProcessingThreads.
                this.currentNumberOfProcessingThreads = 0;
                using (AutoResetEvent doneProcessing = new AutoResetEvent(false))
                {
                    foreach (KeyValuePair<DeviceImportRecord, CustomFieldValueCollection> kvp in this.CustomFieldsToUpdate)
                    {
                        while (currentNumberOfProcessingThreads >= maxNumberOfProcessingThreads)
                            doneProcessing.WaitOne(1000);

                        Interlocked.Increment(ref this.currentNumberOfProcessingThreads);

                        UpdateCustomFieldParams updateCustomFieldParams = new UpdateCustomFieldParams();
                        updateCustomFieldParams.deviceImportRecord = kvp.Key;
                        updateCustomFieldParams.customFieldValueCollection = kvp.Value;
                        updateCustomFieldParams.doneProcessing = doneProcessing;

                        Thread thread = new Thread(new ParameterizedThreadStart(this.UpdateCustomField));
                        thread.IsBackground = true;
                        thread.Name = string.Format("UpdateCustomField - {0}", currentNumberOfProcessingThreads);
                        thread.Start(updateCustomFieldParams);
                    }// end foreach()

                    // wait for our worker threads to finish.
                    DateTime waitForThreadsToFinishUntil = DateTime.Now.AddMinutes(5);
                    while (this.currentNumberOfProcessingThreads > 0 && DateTime.Now < waitForThreadsToFinishUntil)
                        doneProcessing.WaitOne(1000);

                }
                if (currentNumberOfProcessingThreads > 0)
                    throw new IntegrationException(string.Format("Error: Timeout occured while waiting for {0} of our process threads to finish Updating Custom Fields.", currentNumberOfProcessingThreads));
            }
        }

        protected void ProcessNewDevices(string modelNumber, string locationID, string reason, string filePath)
        {
            StringBuilder message = new StringBuilder();
            bool errorDuringProcessing = false;

            message.AppendFormat("ProcessNewDevices model={0} location={1} reason={2}\r\n", modelNumber, locationID, reason);
            try
            {
                int reasonID = this.l_01_utils.GetReasonID(((int)LookupLists.NewStockReceiveReasons).ToString(), reason);
                StockReceiveDetails details = new StockReceiveDetails();
                details.FromStockHanderId = this.l_01_utils.GetStockHandlerID(XmlUtilities.SafeSelect(WorkerSettings, "ManufacturerStockHandlerID").InnerText, externalStockHandlerID);
                details.ToStockHanderId = this.l_01_utils.GetStockHandlerID(locationID, externalStockHandlerID);
                if (details.ToStockHanderId == 0)
                {
                    message.AppendFormat("EC_8 LocationID {0} is not a valid StockHandler in IBS \r\n", locationID);
                }
                details.Reason = reasonID;
                details.UseRangeToDetermineModel = false;
                details = GetIBSDevicesService().CreateStockReceiveDetails(details);

                message.AppendFormat("details.FromStockHanderId={0}  \r\n" +
                    "details.ToStockHanderId={1} \r\n" +
                    "details.Reason={2}  \r\n", details.FromStockHanderId, details.ToStockHanderId, details.Reason);

                BuildList buildList = new BuildList();
                buildList.ModelId = this.l_01_utils.GetHardwareModelID(modelNumber);
                if (buildList.ModelId == 0)
                {
                    message.AppendFormat("EC_8 ModelNumber {0} is not a valid Model in IBS \r\n", modelNumber);
                }
                buildList.Reason = reasonID;
                buildList.StockReceiveDetailsId = details.Id.Value;
                buildList.TransactionType = BuildListTransactionType.ReceiveNewStock;
                buildList = GetIBSDevicesService().CreateBuildList(buildList);

                message.AppendFormat("buildList.ModelId={0}  \r\n" +
                    "buildList.Reason={1}  \r\n" +
                    "buildList.StockReceiveDetailsId={2}  \r\n" +
                    "buildList.TransactionType={3}  \r\n", buildList.ModelId, buildList.Reason, buildList.StockReceiveDetailsId, buildList.TransactionType);

                Diagnostics.TraceInformation(message.ToString());

                // TJohnson - 10/26/2009 - IBSO 15738 - switching over to asynch core calls
                DeviceFileUploadRequest deviceFileUploadRequest = new DeviceFileUploadRequest();
                deviceFileUploadRequest.BuildListId = buildList.Id.Value;
                deviceFileUploadRequest.FileName = filePath;
                int addDevicesToBuildListScheduleID = GetIBSDevicesService().ScheduleAddDevicesToBuildListFromFile(deviceFileUploadRequest);
                int scheduleAddTimeout = int.Parse(XmlUtilities.SafeSelectText(this.WorkerSettings, "ScheduleAddTimeout"));
                int schedulePerformTimeout = int.Parse(XmlUtilities.SafeSelectText(this.WorkerSettings, "SchedulePerformTimeout"));
                WaitForScheduledAction(addDevicesToBuildListScheduleID, buildList.Id.Value,
                    new TimeSpan(0, scheduleAddTimeout, 0),
                    new TimeSpan(0, 0, 10),
                    "ScheduleAddDevicesToBuildListFromFile");

                int performBuildListActionScheduleID = GetIBSDevicesService().SchedulePerformBuildListAction(buildList.Id.Value);
                WaitForScheduledAction(performBuildListActionScheduleID, buildList.Id.Value,
                    new TimeSpan(0, schedulePerformTimeout, 0),
                    new TimeSpan(0, 0, 10),
                    "SchedulePerformBuildListAction");

                #region process failures

                BuildListItemCollection failedItems = GetIBSDevicesService().GetFailedList(buildList.Id.Value, 1);

                foreach (BuildListItem item in failedItems.Items)
                {
                    errorDuringProcessing = true;
                    DeviceImportRecord deviceImportRecord;
                    string errorRecord = string.Empty;

                    if (serialNumbers.TryGetValue(item.SerialNumber, out deviceImportRecord) == true)
                    {
                        errorRecord = BuildErrorRecord(deviceImportRecord);
                        errorRecord += item.Error;
                        message.AppendFormat(". Faild item for record number {0} = {1}  \r\n", deviceImportRecord.recordNumber, item.Error);
                    }
                    else
                    {
                        errorRecord += item.Error;

                        // note: I'm using the GetValueOrDefault() method in a blind attempt to limit the chance of generating errors while trying to log this error.
                        message.AppendFormat(".\r\nBuildListItem falied with the error: {0}\r\nThe BuildListItem.SerialNumnber '{1}' was NOT found in the ICs processing list so we are unable to report the original record number.\r\nHere is the Build List information reported in the error from IBS Core: \r\nBuildListItem.BuildListId '{2}'\r\nBuildListItem.Id '{3}'\r\nBuildListItem.SerialNumnber '{1}'\r\nBuildListItem.DeviceId '{4}'\r\nBuildListItem.ModelId '{5}'\r\nBuildListItem.StatusId '{6}'\r\nBuildListItem.StockHandlerId '{7}'\r\nBuildListItem.Reason '{8}'\r\n",
                            item.Error, item.SerialNumber, item.BuildListId, item.Id.GetValueOrDefault(), item.DeviceId.GetValueOrDefault(), item.ModelId.GetValueOrDefault(), item.StatusId.GetValueOrDefault(), item.StockHandlerId.GetValueOrDefault(), item.Reason.GetValueOrDefault());
                    }

                    WriteToFile(errorFileName, errorRecord);
                    Interlocked.Increment(ref failedRecords);
                }
                if (errorDuringProcessing)
                    Diagnostics.TraceError(message.ToString());
                else
                    Diagnostics.TraceInformation(message.ToString());

                #endregion
            }
            catch (Exception ex)
            {
                errorRecord = "EC_16d An unexpected error occurred while trying to processing BuildList file '" + filePath + "' " + ex.ToString();
                WriteToFile(errorFileName, errorRecord);
                Interlocked.Increment(ref failedRecords);
                Diagnostics.TraceError(message + Environment.NewLine + errorRecord);
            }
        }

        private void ProcessUpdateDevices(DeviceImportRecord deviceRecord, DeviceType deviceType)
        {
            string errorRecord;
            IDevicesService deviceService = GetIBSDevicesService();
            Device device = null;
            try
            {
                int stockHandlerID = this.l_01_utils.GetStockHandlerID(deviceRecord.LocationID, externalStockHandlerID);
                if (stockHandlerID == 0)
                {
                    errorRecord = BuildErrorRecord(deviceRecord);
                    errorRecord += "---" + string.Format("\r\nEC_8 LocationID {0} is not a valid StockHandler in IBS \r\n", deviceRecord.LocationID);
                    WriteToFile(errorFileName, errorRecord);
                    Interlocked.Increment(ref failedRecords);
                }
                else
                {
                    if (deviceType == DeviceType.SetupBox)
                    {
                        device = deviceService.GetDeviceBySerialNumber(deviceRecord.SerialNumber);
                    }
                    else
                    {
                        device = deviceService.GetDeviceBySerialNumber(deviceRecord.SmartCardSerialToPair);
                    }

                    if (device.StockHandlerId.Value == stockHandlerID)
                    {
                        //update device status
                        deviceService.UpdateDeviceStatus(device.Id.Value,
                            this.l_01_utils.GetReasonID(
                            ((int)PayMedia.ApplicationServices.Devices.ServiceContracts.LookupLists.ChangeDeviceStatusReasons).ToString(),
                            deviceRecord.DeviceStatusCode));
                    }
                    else
                    {
                        // move device
                        deviceService.MoveDevice(device.Id.Value,
                            stockHandlerID,
                            0, this.l_01_utils.GetReasonID(
                            ((int)PayMedia.ApplicationServices.Devices.ServiceContracts.LookupLists.TransferDeviceAtStockHandlerReason).ToString(),
                            deviceRecord.DeviceStatusCode),
                            DateTime.Now);

                    }
                }
            }
            catch (Exception ex)
            {
                errorRecord = BuildErrorRecord(deviceRecord);
                errorRecord += "---" + ex.Message;

                WriteToFile(errorFileName, errorRecord);
                Interlocked.Increment(ref failedRecords);

                Diagnostics.TraceError("Update Devices  \r\n" + errorRecord);
            }
        }

        private void ProcessPairedDevices(object obj)
        {
            DeviceImportRecordThreadParam deviceImportRecordThreadParam = null;
            try
            {
                if (obj == null)
                {
                    Diagnostics.TraceError(string.Format("ERROR: {0}() was passed a NULL parameter.  Please report this error to Irdeto BSS Integration team.\r\n", MethodBase.GetCurrentMethod().Name));
                    return;
                }

                if ((obj is DeviceImportRecordThreadParam) == false)
                {
                    Diagnostics.TraceError(string.Format("ERROR: {0}() was passed a parameter of type '{1}', but was expecting type '{2}'.  Please report this error to Irdeto BSS Integration team.\r\n", MethodBase.GetCurrentMethod().Name, obj.GetType().Name, typeof(DeviceImportRecordThreadParam).Name));
                    return;
                }

                deviceImportRecordThreadParam = (DeviceImportRecordThreadParam)obj;
                PageCriteria pageCriteria = new PageCriteria(1);

                int reasonID = this.l_01_utils.GetReasonID(((int)LookupLists.PairDevicesReasons).ToString(), deviceImportRecordThreadParam.deviceImportRecord.DeviceStatusCode);
                if (reasonID != 0)
                {
                    IDevicesService deviceService = GetIBSDevicesService();
                    Device fromDevice = deviceService.GetDeviceBySerialNumber(deviceImportRecordThreadParam.deviceImportRecord.SerialNumber);
                    if (fromDevice != null)
                    {
                        PairingCollection fromPairCol = deviceService.GetPairings(fromDevice.Id.Value, pageCriteria);

                        if (fromPairCol.Items.Count > 0 && fromPairCol.Items[0].PairedToSerialNumber != deviceImportRecordThreadParam.deviceImportRecord.SmartCardSerialToPair)
                        {
                            ReportError(deviceImportRecordThreadParam.deviceImportRecord, "EC_15" + this.delimiterString +
                                "Device " + fromPairCol.Items[0].PairedFromSerialNumber + " is currently paired with device " +
                                fromPairCol.Items[0].PairedToSerialNumber + " and cannot be repaired");
                        }
                        else if (fromPairCol.Items.Count == 0)
                        {
                            Device toDevice = deviceService.GetDeviceBySerialNumber(deviceImportRecordThreadParam.deviceImportRecord.SmartCardSerialToPair);
                            if (toDevice != null)
                            {
                                PairingCollection toPairCol = deviceService.GetPairings(fromDevice.Id.Value, pageCriteria);
                                if (toPairCol.Items.Count > 0 && toPairCol.Items[0].PairedFromSerialNumber != deviceImportRecordThreadParam.deviceImportRecord.SerialNumber)
                                {
                                    ReportError(deviceImportRecordThreadParam.deviceImportRecord, "EC_15" + this.delimiterString +
                                        "Device " + toPairCol.Items[0].PairedFromSerialNumber + " is currently paired with device " +
                                        toPairCol.Items[0].PairedToSerialNumber + " and cannot be repaired");
                                }
                                else
                                {
                                    GetIBSDevicesService().PairOneDeviceToAnother(fromDevice.Id.Value, toDevice.Id.Value, reasonID);
                                }
                            }
                            else
                            {
                                ReportError(deviceImportRecordThreadParam.deviceImportRecord, string.Format("EC_14 Device not found by serial number {0}", deviceImportRecordThreadParam.deviceImportRecord.SmartCardSerialToPair));
                            }
                        }
                    }
                    else
                    {
                        ReportError(deviceImportRecordThreadParam.deviceImportRecord, string.Format("EC_14 Device not found by serial number {0}", deviceImportRecordThreadParam.deviceImportRecord.SerialNumber));
                    }
                }
                else
                {
                    ReportError(deviceImportRecordThreadParam.deviceImportRecord, string.Format("Reason {0} not defined in IBS", deviceImportRecordThreadParam.deviceImportRecord.DeviceStatusCode));
                }
            }
            catch (Exception ex)
            {
                if (deviceImportRecordThreadParam == null)
                    Diagnostics.TraceError("Unexpected error occured: " + ex.ToString());
                else
                    ReportError(deviceImportRecordThreadParam.deviceImportRecord, "EC_16e An unexpected error occurred. " + ex.ToString());
            }
            finally
            {
                Interlocked.Decrement(ref this.currentNumberOfProcessingThreads);
                if (deviceImportRecordThreadParam != null)
                    deviceImportRecordThreadParam.doneProcessing.Set();
            }
        }

        protected void WaitForScheduledAction(int scheduleID, int buildListID, TimeSpan timeout, TimeSpan pollInterval, string actionDescription)
        {
            bool unknownStatusReported = false;
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime.Add(timeout);
            bool succeeded = false;

            while (DateTime.Now < startTime.Add(timeout) && !succeeded)
            {
                ScheduleHeader header = GetScheduleManagerService().GetScheduleHeader(scheduleID);
                switch (header.Status)
                {
                    case ScheduleStatus.Pending:
                    case ScheduleStatus.Enqueued:
                        Thread.CurrentThread.Join(pollInterval);
                        unknownStatusReported = false; // reset our flag just in case we receive a new unknown status.
                        break;

                    case ScheduleStatus.Canceled:
                        throw new IntegrationException(string.Format("While performing action {0}, Buildlist ID {1} with schedule ID {2} has been cancelled in core.",
                            actionDescription,
                            buildListID.ToString(),
                            scheduleID.ToString()));

                    case ScheduleStatus.Completed:
                        succeeded = true;
                        break;

                    case ScheduleStatus.Failed:
                        throw new IntegrationException(string.Format("While performing action {0}, Buildlist ID {1} with schedule ID {2} has failed in core.  Error detail: {3}",
                            actionDescription,
                            buildListID.ToString(),
                            scheduleID.ToString(),
                            header.Message));

                    default:
                        // 2011.08.22 - JCopus
                        // if we received a status that we do NOT know about then log it and continue like nothing is wrong.
                        // note: We do this because Core added the Enqueue status without telling the IC and the IC then started
                        //       calling Core at full speed since it didn't know about the new status. 
                        //       If the IC terminates with an errro in when we see an unknown status then the client would blame the IC. 
                        //       So we will continue to run and only warn.  
                        //       With a little luck our QA will see this message and inform the developers so that the client never sees the warning.
                        if (unknownStatusReported == false)
                        {
                            string warning = string.Format("Warning: Unknown ScheduleStatus of '{0}' returned from ScheduleManagerService.GetScheduleHeader(). The IC will continue to wait for a known ScheduleStatus until ", Enum.GetName(typeof(ScheduleStatus), header.Status));
                            Diagnostics.TraceWarning(warning);
                            unknownStatusReported = true;
                        }
                        Thread.CurrentThread.Join(pollInterval);
                        break;
                }
            }
            if (!succeeded)
                throw new IntegrationException(string.Format("While performing action {0}, Buildlist ID {1} with schedule ID {2} has timed out after {3} seconds.",
                    actionDescription,
                    buildListID.ToString(),
                    scheduleID.ToString(),
                    timeout.TotalSeconds.ToString()));
        }

        #endregion

        #region Private Methods

        private void InitializeLocalCollections()
        {
            this.l_01_utils = new L_01_Utils(Configuration.AppSettings.IntegrationDataDsn, this.externalStockHandlerID);

            DevicesList = new List<DeviceImportRecord>();
            serialNumbers = new Dictionary<string, DeviceImportRecord>();
            NewDevices = new List<DeviceImportRecord>();
            UpdateDevices = new List<DeviceImportRecord>();
            NewSmartCards = new List<DeviceImportRecord>();
            UpdateSmartCards = new List<DeviceImportRecord>();
            PairedDevices = new List<DeviceImportRecord>();
            DeviceLocIdModels = new Dictionary<LocationIDModelName, string>();
            CustomFieldsToUpdate = new Dictionary<DeviceImportRecord, CustomFieldValueCollection>();
            SmartCardLocIdModels = new Dictionary<LocationIDModelName, string>();
        }

        private void GetConfigValues(XmlNode workerSettings)
        {
            // Retrieve the config args.
            buildListImportPath = XmlUtilities.SafeSelect(workerSettings, "BuildListImportPath").InnerText;
            deviceTypePaired = XmlUtilities.SafeSelect(workerSettings, "DeviceTypePaired").InnerText;
            externalStockHandlerID = XmlUtilities.SafeSelect(workerSettings, "LocationIDCustomField").InnerText;
            ftpUrl = XmlUtilities.SafeSelect(workerSettings, "ftpUrl").InnerText;
            userName = XmlUtilities.SafeSelect(workerSettings, "UserName").InnerText;
            pass = XmlUtilities.SafeSelect(workerSettings, "Pass").InnerText;

            // 2010.04.08 JCopus - an optional configuration field that I don't really intend to tell the client about.
            maxNumberOfProcessingThreads = Convert.ToInt32(XmlUtilities.SafeSelectText(workerSettings, "maxNumberOfProcessingThreads", "5"));
        }

        private void InitializeLogRecord()
        {
            logRecord = new DeviceImportLogRecord();
            logRecord.ID = 0;
            logRecord.RunID = 0;
            logRecord.FixedRunID = 0;
            logRecord.SourceURI = string.Empty;
            logRecord.ArchivePath = string.Empty;
            logRecord.SucceededCount = 0;
            logRecord.FailedCount = 0;
        }

        private void AddRecordToDevicesFile(DeviceImportRecord deviceRecord)
        {
            string filePath;
            string record;

            LocationIDModelName locationIdModelName = new LocationIDModelName();

            locationIdModelName.LocationID = deviceRecord.LocationID;
            locationIdModelName.ModelName = deviceRecord.ModelName;
            locationIdModelName.DeviceStatusCode = deviceRecord.DeviceStatusCode;

            record = deviceRecord.SerialNumber + " " + deviceRecord.ChipsetID;
            DeviceLocIdModels.TryGetValue(locationIdModelName, out filePath);

            WriteToFile(filePath, record);
        }

        private void AddRecordToSmartCardsFile(DeviceImportRecord deviceRecord)
        {
            string filePath;
            string record;

            LocationIDModelName locationIdModelName = new LocationIDModelName();
            locationIdModelName.LocationID = deviceRecord.LocationID;
            locationIdModelName.ModelName = deviceRecord.SmartCardToPairModelName;
            locationIdModelName.DeviceStatusCode = deviceRecord.DeviceStatusCode;

            record = deviceRecord.SmartCardSerialToPair;
            SmartCardLocIdModels.TryGetValue(locationIdModelName, out filePath);

            WriteToFile(filePath, record);
        }

        private void LoadDeviceIntoDictionaries(object parms)
        {
            DeviceImportRecordThreadParam myparams = null;
            try
            {
                if (parms == null)
                {
                    Diagnostics.TraceError(string.Format("ERROR: {0}() was passed a NULL parameter.  Please report this error to Irdeto BSS Integration team.\r\n", MethodBase.GetCurrentMethod().Name));
                    return;
                }

                if ((parms is DeviceImportRecordThreadParam) == false)
                {
                    Diagnostics.TraceError(string.Format("ERROR: {0}() was passed a parameter of type '{1}', but was expecting type '{2}'.  Please report this error to Irdeto BSS Integration team.\r\n", MethodBase.GetCurrentMethod().Name, parms.GetType().Name, typeof(DeviceImportRecordThreadParam).Name));
                    return;
                }

                myparams = (DeviceImportRecordThreadParam)parms;
                DeviceImportRecord deviceImportRecord = myparams.deviceImportRecord;

                try
                {
                    #region Get LocationIDModelName

                    LocationIDModelName deviceLocationIdModelName = new LocationIDModelName();

                    if (deviceImportRecord.DeviceType == deviceTypePaired)
                    {
                        Device pairedDevice = GetIBSDevicesService().GetDeviceBySerialNumber(deviceImportRecord.SerialNumber);

                        if (pairedDevice == null)
                        {
                            deviceLocationIdModelName.LocationID = deviceImportRecord.LocationID;
                            deviceLocationIdModelName.ModelName = deviceImportRecord.ModelName;
                            deviceLocationIdModelName.DeviceStatusCode = deviceImportRecord.DeviceStatusCode;
                            AddDeviceLocationModel(deviceLocationIdModelName);
                            lock (NewDevices)
                            {
                                NewDevices.Add(deviceImportRecord);
                            }
                        }
                        else
                        {
                            //check here if it needs to be updated
                            lock (UpdateDevices)
                            {
                                UpdateDevices.Add(deviceImportRecord);
                            }
                        }

                        Device tmpSC = GetIBSDevicesService().GetDeviceBySerialNumber(deviceImportRecord.SmartCardSerialToPair);

                        if (tmpSC == null)
                        {
                            LocationIDModelName smartCardLocationIdModelName = new LocationIDModelName();
                            smartCardLocationIdModelName.LocationID = deviceImportRecord.LocationID;
                            smartCardLocationIdModelName.ModelName = deviceImportRecord.SmartCardToPairModelName;
                            smartCardLocationIdModelName.DeviceStatusCode = deviceImportRecord.DeviceStatusCode;
                            AddSmartCardLocationModel(smartCardLocationIdModelName);
                            lock (NewSmartCards)
                            {
                                NewSmartCards.Add(deviceImportRecord);
                            }
                        }
                        else
                        {
                            //check here if it needs to be updated
                            lock (UpdateSmartCards)
                            {
                                UpdateSmartCards.Add(deviceImportRecord);
                            }
                        }

                        lock (PairedDevices)
                        {
                            PairedDevices.Add(deviceImportRecord);
                        }
                    }
                    else
                    {

                        Device tmpDevice = GetIBSDevicesService().GetDeviceBySerialNumber(deviceImportRecord.SerialNumber);

                        if (tmpDevice == null)
                        {
                            deviceLocationIdModelName.LocationID = deviceImportRecord.LocationID;
                            deviceLocationIdModelName.ModelName = deviceImportRecord.ModelName;
                            deviceLocationIdModelName.DeviceStatusCode = deviceImportRecord.DeviceStatusCode;
                            AddDeviceLocationModel(deviceLocationIdModelName);

                            lock (NewDevices)
                            {
                                NewDevices.Add(deviceImportRecord);
                            }
                        }
                        else
                        {
                            //check here if it needs to be updated
                            lock (UpdateDevices)
                            {
                                UpdateDevices.Add(deviceImportRecord);
                            }
                        }
                    }

                    #endregion
                }
                catch (Exception ex)
                {
                    Diagnostics.TraceError(ex.ToString() + string.Format("\r\ndeviceImportRecord.recordNumber: '{0}', deviceImportRecord.SerialNumber: '{1}'\r\n", deviceImportRecord.recordNumber, deviceImportRecord.SerialNumber));
                }
            }
            catch (Exception ex)
            {
                Diagnostics.TraceError("Unexpected error occured: " + ex.ToString());
            }
            finally
            {
                Interlocked.Decrement(ref this.currentNumberOfProcessingThreads);
                if (myparams != null)
                    myparams.doneProcessing.Set();
            }
        }

        private void AddDeviceLocationModel(LocationIDModelName locationIdModelName)
        {
            string tempString;

            lock (DeviceLocIdModels)
            {
                DeviceLocIdModels.TryGetValue(locationIdModelName, out tempString);
                if (tempString == null)
                {
                    string fileName = Path.Combine(buildListImportPath,
                        "BuildList_" + locationIdModelName.LocationID + "_" +
                        locationIdModelName.ModelName + "." + Guid.NewGuid().ToString() + ".txt");

                    DeviceLocIdModels.Add(locationIdModelName, fileName);
                }
            }
        }

        private void AddSmartCardLocationModel(LocationIDModelName locationIdModelName)
        {
            string tempString;

            lock (SmartCardLocIdModels)
            {
                SmartCardLocIdModels.TryGetValue(locationIdModelName, out tempString);
                if (tempString == null)
                {
                    string fileName = Path.Combine(buildListImportPath,
                        "BuildList_" + locationIdModelName.LocationID + "_" +
                        locationIdModelName.ModelName + "." + Guid.NewGuid().ToString() + ".txt");

                    SmartCardLocIdModels.Add(locationIdModelName, fileName);
                }
            }
        }

        private void UpdateCustomField(object obj)
        {
            UpdateCustomFieldParams updateCustomFieldParams = null;
            try
            {
                if (obj == null)
                {
                    Diagnostics.TraceError(string.Format("ERROR: {0}() was passed a NULL parameter.  Please report this error to Irdeto BSS Integration team.\r\n", MethodBase.GetCurrentMethod().Name));
                    return;
                }

                if ((obj is UpdateCustomFieldParams) == false)
                {
                    Diagnostics.TraceError(string.Format("ERROR: {0}() was passed a parameter of type '{1}', but was expecting type '{2}'.  Please report this error to Irdeto BSS Integration team.\r\n", MethodBase.GetCurrentMethod().Name, obj.GetType().Name, typeof(UpdateCustomFieldParams).Name));
                    return;
                }

                updateCustomFieldParams = (UpdateCustomFieldParams)obj;

                string serialNumber = updateCustomFieldParams.deviceImportRecord.SerialNumber;
                string secondSerialNumber = updateCustomFieldParams.deviceImportRecord.SmartCardSerialToPair;

                // the calls to the IbsCoreUtilities really like and need the MessageContext to be set to our IntegrationMailMessage.
                // note: I could extend our core wrapper to take a dsn, but I'm being lazy today to I will simply set the context on this thread.
                //using (new MessageContext(this.BaseMailMessage))
                //{
                    
                //}
                Device device = DeviceUtilities.GetDeviceBySerialNumber(serialNumber);
                if (device != null)
                {
                    CustomFieldUtilities.UpdateCustomFieldValues(device.Id.GetValueOrDefault(), Device.ENTITY_ID, updateCustomFieldParams.customFieldValueCollection);
                }
                if (!string.IsNullOrEmpty(secondSerialNumber))
                {
                    Device secondDevice = DeviceUtilities.GetDeviceBySerialNumber(secondSerialNumber);
                    if (secondDevice != null)
                    {
                        CustomFieldUtilities.UpdateCustomFieldValues(secondDevice.Id.GetValueOrDefault(), Device.ENTITY_ID, updateCustomFieldParams.customFieldValueCollection);
                    }
                }
            }
            catch (Exception ex)
            {
                if (updateCustomFieldParams == null)
                {
                    Diagnostics.TraceError("Unexpected error occured: " + ex.ToString());
                }
                else
                {
                    string errorRecord;

                    errorRecord = BuildErrorRecord(updateCustomFieldParams.deviceImportRecord);
                    errorRecord += "---" + ex.Message;

                    WriteToFile(errorFileName, errorRecord);
                    Interlocked.Increment(ref failedRecords);

                    Diagnostics.TraceError("Update Device Custom Fields  \r\n" + errorRecord);
                }
            }
            finally
            {
                Interlocked.Decrement(ref this.currentNumberOfProcessingThreads);
                if (updateCustomFieldParams != null)
                    updateCustomFieldParams.doneProcessing.Set();
            }
        }

        private void ReportError(DeviceImportRecord deviceRecord, string message)
        {
            string errorRecord = BuildErrorRecord(deviceRecord);
            errorRecord += "---" + message;
            WriteToFile(errorFileName, errorRecord);
            Interlocked.Increment(ref this.failedRecords);
            Diagnostics.TraceError(errorRecord);
        }

        private string BuildErrorRecord(DeviceImportRecord deviceRecord)
        {
            string errorRecord = deviceRecord.SerialNumber + delimiterString +
                            deviceRecord.DeviceType + delimiterString +
                            deviceRecord.ModelName + delimiterString +
                            deviceRecord.StockHandlerType + delimiterString +
                            deviceRecord.LocationID + delimiterString +
                            deviceRecord.DeviceStatusCode + delimiterString +
                            deviceRecord.ChipsetID + delimiterString +
                            deviceRecord.SmartCardSerialToPair + delimiterString +
                            deviceRecord.SmartCardToPairModelName +
                            deviceRecord.CustomFields;
            return errorRecord;
        }

        #endregion

    }
}
