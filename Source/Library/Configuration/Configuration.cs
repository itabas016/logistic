using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using PayMedia.Integration.FrameworkService.Interfaces.Common;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public class Configuration
    {
        #region Private Fields

        private static readonly object generalConfigLocker;
        private static readonly object locker;
        private static readonly object workerConfigLocker;
        private static readonly object ftpwatcherLocker;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the <see cref="Configuration"/> class.
        /// </summary>
        static Configuration()
        {
            locker = new object();
            generalConfigLocker = new object();
            workerConfigLocker = new object();
            ftpwatcherLocker = new object();
        }

        /// <summary>
        /// when use configuration, must be call this method, init configurations in cache
        /// </summary>
        /// <param name="componentInitContext"></param>
        public static void Init(IComponentInitContext componentInitContext)
        {
            GetApplicationSettings(componentInitContext);

            GetWorkerConfiguration(componentInitContext);

            GetFtpWatcherConfiguration(componentInitContext);

            GetGeneralConfiguration(componentInitContext);
        }

        /// <summary>
        /// Gets the current application's settings.
        /// </summary>
        public static ApplicationSettings AppSettings
        {
            get
            {
                ApplicationSettings appSettings = (ApplicationSettings)Cache.Get(Const.CACHE_KEY_APPLICATION_SETTINGS);

                return appSettings;
            }
        }

        public static XmlNode WorkerSetting
        {
            get { return (XmlNode)Cache.Get(Const.CACHE_KEY_WORKER_SETTINGS); }
        }

        public static FtpWatcherConfiguration FtpWatcherSetting
        {
            get { return (FtpWatcherConfiguration) Cache.Get(Const.CACHE_KEY_FTPWATCHER_SETTINGS); }
        }

        public static Dictionary<string, Dictionary<string, string>> GeneralSetting
        {
            get { return (Dictionary<string, Dictionary<string, string>>) Cache.Get(Const.CACHE_KEY_GENERAL_SETTINGS); }
        }

        /// <summary>
        /// Get config from component propery config key is "application_setting"
        /// </summary>
        /// <param name="componentInitContext"></param>
        /// <returns></returns>
        public static ApplicationSettings GetApplicationSettings(IComponentInitContext componentInitContext)
        {
            ApplicationSettings applicationSettings;

            lock (locker)
            {
                try
                {
                    applicationSettings = (ApplicationSettings)Cache.Get(Const.CACHE_KEY_APPLICATION_SETTINGS);
                }
                catch (KeyNotFoundException)
                {
                    if (componentInitContext == null | string.IsNullOrEmpty(componentInitContext.Config[Const.PROP_APPLICATION_SETTING]))
                    {
                        throw new KeyNotFoundException(
                            string.Format(
                                "The config key {0} was not found in component property.", Const.PROP_APPLICATION_SETTING));
                    }
                    applicationSettings = LoadApplicationSettings(componentInitContext.Config[Const.PROP_APPLICATION_SETTING]);
                    Cache.Add(Const.CACHE_KEY_APPLICATION_SETTINGS, applicationSettings);
                }
            }
            return applicationSettings;
        }

        /// <summary>
        /// Gets a general configuration value.
        /// default load local config xml for WOD FTP property
        /// </summary>
        /// <param name="groupName">The group name under which the config item resides.</param>
        /// <param name="keyName">The name of the config item.</param>
        /// <returns>The config value.</returns>
        public static string GetGeneralConfigValue(string groupName, string keyName)
        {
            // Get the config dictionary.
            Dictionary<string, Dictionary<string, string>> generalConfiguration;
            lock (generalConfigLocker)
            {
                try
                {
                    generalConfiguration = (Dictionary<string, Dictionary<string, string>>)Cache.Get(Const.CACHE_KEY_GENERAL_SETTINGS);
                }
                catch (KeyNotFoundException)
                {
                    // Load and cache the configuration.
                    generalConfiguration = GeneralSetting;
                    Cache.Add(Const.CACHE_KEY_GENERAL_SETTINGS, generalConfiguration);
                }
            }

            // Get the group's configuration.
            Dictionary<string, string> groupConfig = generalConfiguration[groupName];

            if (groupConfig != null)
            {
                return groupConfig[keyName];
            }

            return string.Empty;
        }

        /// <summary>
        /// Get config from component propery config key is "worker_setting"
        /// </summary>
        /// <returns></returns>
        public static XmlNode GetWorkerConfiguration(IComponentInitContext componentInitContext)
        {
            XmlNode workerSettingXmlNode;

            lock (workerConfigLocker)
            {
                try
                {
                    workerSettingXmlNode = (XmlNode)Cache.Get(Const.CACHE_KEY_WORKER_SETTINGS);
                }
                catch (KeyNotFoundException)
                {
                    if (componentInitContext == null | string.IsNullOrEmpty(componentInitContext.Config[Const.PROP_WORKER_SETTING]))
                    {
                        throw new KeyNotFoundException(
                            string.Format(
                                "The config key {0} was not found in component property.", Const.PROP_WORKER_SETTING));
                    }
                    // Load and cache the configuration.
                    workerSettingXmlNode = LoadWorkerConfiguration(componentInitContext.Config[Const.PROP_WORKER_SETTING]);
                    Cache.Add(Const.CACHE_KEY_WORKER_SETTINGS, workerSettingXmlNode);
                }
            }
            return workerSettingXmlNode;
        }

        public static FtpWatcherConfiguration GetFtpWatcherConfiguration(IComponentInitContext componentInitContext)
        {
            FtpWatcherConfiguration watcher;

            lock (ftpwatcherLocker)
            {
                try
                {
                    watcher = (FtpWatcherConfiguration)Cache.Get(Const.CACHE_KEY_FTPWATCHER_SETTINGS);
                }
                catch (KeyNotFoundException)
                {
                    var workerSetting = GetWorkerConfiguration(componentInitContext);

                    watcher = new FtpWatcherConfiguration();
                    watcher.Name = XmlUtilities.SafeSelect(workerSetting, "MessageName").InnerText;
                    watcher.DeleteAfterDownloading = ValidationUtilities.ParseBool(XmlUtilities.SafeSelect(workerSetting, "DeleteAfterDownloading").InnerText);
                    watcher.PollingFileExtensions = ValidationUtilities.Split(XmlUtilities.SafeSelect(workerSetting, "FileExtensionList").InnerText, 1, true, ",");
                    watcher.PollingEndpoint = new FtpEndpoint();
                    watcher.PollingEndpoint.Name = watcher.Name;
                    watcher.PollingEndpoint.Address = XmlUtilities.SafeSelect(workerSetting, "ftpUrl").InnerText;
                    watcher.PollingEndpoint.Username = XmlUtilities.SafeSelect(workerSetting, "UserName").InnerText;
                    watcher.PollingEndpoint.Password = XmlUtilities.SafeSelect(workerSetting, "Pass").InnerText;
                    watcher.PollingEndpoint.TransferInterval = new TimeSpan(0, 0, 0, 0, ValidationUtilities.ParseInt(XmlUtilities.SafeSelect(workerSetting, "TransferIntervalMs").InnerText));
                    watcher.PollingEndpoint.InTransitFileExtension = XmlUtilities.SafeSelect(workerSetting, "TransitFileExtension").InnerText;

                    // Storage values.
                    watcher.StorageFilePath = XmlUtilities.SafeSelect(workerSetting, "StoragePath").InnerText;

                    // Forwarding values.
                    //watcher.ForwardingEndpoint = CreateEndpoint(ValidationUtilities.ParseInt(reader, "ENDPOINT_TYPE_ID"));
                    //watcher.ForwardingEndpoint.InitFromDataReader(reader);
                    //string dsn = ValidationUtilities.ParseString(reader, "CONTEXT_DSN");
                    //string conditionName = ValidationUtilities.ParseString(reader, "CONTEXT_CONDITION_NAME");
                    //string conditionValue = ValidationUtilities.ParseString(reader, "CONTEXT_CONDITION_VALUE");
                    //watcher.ForwardingMailMessage = new IntegrationMailMessage(0, conditionName, conditionValue, 0, dsn, string.Empty, null, string.Empty);
                    Cache.Add(Const.CACHE_KEY_FTPWATCHER_SETTINGS, watcher);
                }
            }
            return watcher;
        }

        public static Dictionary<string, Dictionary<string, string>> GetGeneralConfiguration(IComponentInitContext componentInitContext)
        {
            Dictionary<string, Dictionary<string, string>> generalDictionary;

            lock (generalConfigLocker)
            {
                try
                {
                    generalDictionary =
                        (Dictionary<string, Dictionary<string, string>>) Cache.Get(Const.CACHE_KEY_GENERAL_SETTINGS);
                }
                catch (KeyNotFoundException)
                {
                    if (componentInitContext == null | string.IsNullOrEmpty(componentInitContext.Config[Const.PROP_GENERAL_SETTING]))
                    {
                        throw new KeyNotFoundException(
                            string.Format(
                                "The config key {0} was not found in component property.", Const.PROP_GENERAL_SETTING));
                    }
                    // Load and cache the configuration.
                    generalDictionary = LoadGeneralConfiguration(componentInitContext.Config[Const.PROP_GENERAL_SETTING]);
                    Cache.Add(Const.CACHE_KEY_GENERAL_SETTINGS, generalDictionary);
                }
            }
            return generalDictionary;
        }

        /// <summary>
        /// Clear the cached configuration.
        /// </summary>
        public static void ClearConfiguration()
        {
            lock (locker)
            {
                Cache.Remove("AppSettings");
            }
            lock (generalConfigLocker)
            {
                Cache.Remove("GeneralConfiguration");
            }
            lock (workerConfigLocker)
            {
                Cache.Remove("WorkerConfiguration");
            }
            lock (ftpwatcherLocker)
            {
                Cache.Remove("FtpWatcherSetting");
            }
        }

        #endregion

        #region Private Methods

        private static XmlNode LoadWorkerConfiguration(string configuration)
        {
            var workerSettings = XmlUtilities.StringToXmlNode(configuration); //GetFileResource(@"\L_01\worker_setting.xml")
            return workerSettings;
        }

        private static ApplicationSettings LoadApplicationSettings(string configuration)
        {
            ApplicationSettings appSettings = new ApplicationSettings();

            #region Application setting configuration

            var applicationXmlNode = XmlUtilities.StringToXmlNode(configuration); //GetFileResource(@"\L_01\application_setting.xml")
            //appSettings.ApplicationID = ValidationUtilities.ParseInt(XmlUtilities.SafeSelect(applicationXmlNode, "IC_ID").InnerText);
            //appSettings.ApplicationName = appName;
            appSettings.AsmUsername = XmlUtilities.SafeSelect(applicationXmlNode, "ASM_USERNAME").InnerText;
            appSettings.AsmPassword = XmlUtilities.SafeSelect(applicationXmlNode, "ASM_PASSWORD").InnerText;
            appSettings.IntegrationDataDsn = XmlUtilities.SafeSelect(applicationXmlNode, "DSN").InnerText;
            //appSettings.IsCertAuthEnabled = ValidationUtilities.ParseBool(XmlUtilities.SafeSelect(applicationXmlNode, "IS_CERT_AUTH_ENABLED").InnerText);
            //appSettings.ConditionValidatorTypename = XmlUtilities.SafeSelect(applicationXmlNode, "CONDITION_VALIDATOR_TYPENAME").InnerText;
            appSettings.CoreServiceLocator = XmlUtilities.SafeSelect(applicationXmlNode, "ASM_SERVICE_LOCATOR").InnerText;
            //appSettings.TempFolderPath = "";
            //FileUtilities.ResolveSpecialFolderPath(
            //    ValidationUtilities.ParseString(reader, "TEMP_SPECIAL_FOLDER", true),
            //    ValidationUtilities.ParseString(reader, "TEMP_FOLDER_PATH"));
            appSettings.CommunicationLogServiceCache = XmlUtilities.SafeSelect(applicationXmlNode, "COMM_LOG_SERVICE_CACHE").InnerText;
            //appSettings.RequiredCI_CENTRALSchemaVersion = requiredCI_CENTRALSchemaVersion;
            //appSettings.RequiredCI_BUSINESSDATASchemaVersion = requiredCI_BUSINESSDATASchemaVersion;
            //appSettings.CoreVersionCompatibilityFileName = coreVersionCompatibilityFileName;
            //appSettings.DisableVersionCheck = ValidationUtilities.ParseBool(disableVersionCheck);

            return appSettings;

            #endregion
        }

        private static Dictionary<string, Dictionary<string, string>> LoadGeneralConfiguration(string configuration)
        {
            Dictionary<string, Dictionary<string, string>> generalConfiguration =
                new Dictionary<string, Dictionary<string, string>>();

            var generalXmlNode = XmlUtilities.StringToXmlNode(configuration);

            // Get the configuration details.
            string groupName = XmlUtilities.SafeSelectText(generalXmlNode, "//group/@name");

            var groupSettings = XmlUtilities.SafeSelectList(generalXmlNode, "//group//setting");


            // Get the group config dictionary.
            Dictionary<string, string> groupConfiguration;

            try
            {
                groupConfiguration = generalConfiguration[groupName];
            }
            catch (KeyNotFoundException)
            {
                groupConfiguration = new Dictionary<string, string>();
                generalConfiguration.Add(groupName, groupConfiguration);

                if (groupSettings != null && groupSettings.Count > 0)
                {
                    foreach (var item in groupSettings)
                    {
                        string keyName = ((XmlNode)item).Attributes["key"].Value.ToString(); //XmlUtilities.SafeSelectText((XmlNode)item, "//setting/@key");
                        string configValue = ((XmlNode)item).Attributes["value"].Value.ToString(); //XmlUtilities.SafeSelectText((XmlNode)item, "//setting/@value");

                        // Add the new key to the group.
                        if (!groupConfiguration.ContainsKey(keyName))
                            groupConfiguration.Add(keyName, configValue);
                        /*
                        throw new IntegrationException(
                                string.Format(
                                    "The config key {0} exists more than once under group {1} in the general configuration of app {2}.",
                                    keyName, groupName, AppSettings.ApplicationName));
                       */
                    }
                }
            }

            return generalConfiguration;
        }

        private static string GetFileResource(string filePath)
        {
            try
            {
                var fileDirectoryPrefix = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(Configuration)).Location);

                var result = File.ReadAllText(string.Format(@"{0}..\{1}", fileDirectoryPrefix, filePath));
                return result;
            }
            catch (Exception ex)
            {
                throw new IntegrationException(string.Format("Load configuration xml failed. Error Message: {0}", ex.Message));
            }
        }

        #endregion
    }
}
