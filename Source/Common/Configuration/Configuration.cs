using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Logistic.Integration.Common
{
    public class Configuration
    {
        #region Private Fields

        private static readonly object generalConfigLocker;
        private static readonly object locker;
        private static readonly object workerConfigLocker;

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
        }

        /// <summary>
        /// Gets the current application's settings.
        /// </summary>
        public static ApplicationSettings AppSettings
        {
            get
            {
                ApplicationSettings appSettings;

                lock (locker)
                {
                    try
                    {
                        appSettings = (ApplicationSettings)Cache.Get("AppSettings");
                    }
                    catch (KeyNotFoundException)
                    {
                        // Load and cache the settings.
                        appSettings = Configuration.LoadAppSettings();
                        Cache.Add("AppSettings", appSettings);
                    }
                }

                return appSettings;
            }
        }

        /// <summary>
        /// Gets a general configuration value.
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
                    generalConfiguration =
                        (Dictionary<string, Dictionary<string, string>>)Cache.Get("GeneralConfiguration");
                }
                catch (KeyNotFoundException)
                {
                    // Load and cache the configuration.
                    generalConfiguration = LoadGeneralConfiguration();
                    Cache.Add("GeneralConfiguration", generalConfiguration);
                }
            }

            // Get the group's configuration.
            Dictionary<string, string> groupConfig;
            try
            {
                groupConfig = generalConfiguration[groupName];
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException(
                    string.Format("The config group name {0} was not found in general configuration for app {1}.",
                        groupName, AppSettings.ApplicationName));
            }

            // Get the value of the key.
            string value;
            try
            {
                value = groupConfig[keyName];
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException(
                    string.Format(
                        "The config key {0} was not found in general configuration under group {1} for app {2}.",
                        keyName, groupName, AppSettings.ApplicationName));
            }

            return value;
        }

        /// <summary>
        /// Gets a worker's configuration.
        /// </summary>
        /// <param name="workerName">Name of the worker.</param>
        /// <returns></returns>
        public static XmlNode GetWorkerConfiguration()
        {
            return LoadWorkerConfiguration();
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
        }

        #endregion

        #region Private Methods

        private static XmlNode LoadWorkerConfiguration()
        {
            var workerSettings = XmlUtilities.StringToXmlNode(LoadXml(@"\Library\Logistics\L_01\worker_setting.xml"));
            return workerSettings;
        }

        private static Dictionary<string, Dictionary<string, string>> LoadGeneralConfiguration()
        {
            Dictionary<string, Dictionary<string, string>> generalConfiguration =
                new Dictionary<string, Dictionary<string, string>>();

            var generalXmlNode = XmlUtilities.StringToXmlNode(LoadXml(@"\Library\Logistics\L_01\general_configuration_setting.xml"));

            // Get the configuration details.
            string groupName = XmlUtilities.SafeSelect(generalXmlNode, "GROUP_NAME").InnerText;
            string keyName = XmlUtilities.SafeSelect(generalXmlNode, "KEY_NAME").InnerText;
            string configValue = XmlUtilities.SafeSelect(generalXmlNode, "VALUE").InnerText;

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
            }

            // Add the new key to the group.
            if (groupConfiguration.ContainsKey(keyName))
                throw new IntegrationException(
                    string.Format(
                        "The config key {0} exists more than once under group {1} in the general configuration of app {2}.",
                        keyName, groupName, AppSettings.ApplicationName));
            groupConfiguration.Add(keyName, configValue);

            return generalConfiguration;
        }

        private static ApplicationSettings LoadAppSettings()
        {
            ApplicationSettings appSettings = new ApplicationSettings();

            #region App web configuration

            string appName = ConfigurationManager.AppSettings["ApplicationName"];
            if (string.IsNullOrEmpty(appName))
                throw new IntegrationException("Configuration key \"ApplicationName\" not found under appSettings.");

            string requiredCI_CENTRALSchemaVersion = ConfigurationManager.AppSettings["RequiredCI_CENTRALSchemaVersion"];
            if (string.IsNullOrEmpty(requiredCI_CENTRALSchemaVersion))
                throw new IntegrationException("Configuration key \"RequiredCI_CENTRALSchemaVersion\" not found under appSettings.");

            string requiredCI_BUSINESSDATASchemaVersion = ConfigurationManager.AppSettings["RequiredCI_BUSINESSDATASchemaVersion"];
            if (string.IsNullOrEmpty(requiredCI_BUSINESSDATASchemaVersion))
                throw new IntegrationException("Configuration key \"RequiredCI_BUSINESSDATASchemaVersion\" not found under appSettings.");

            string coreVersionCompatibilityFileName = ConfigurationManager.AppSettings["CoreVersionCompatibilityFileName"];
            if (string.IsNullOrEmpty(coreVersionCompatibilityFileName))
                throw new IntegrationException("Configuration key \"CoreVersionCompatibilityFileName\" not found under appSettings.");

            // This is a "hidden" configuration value, by default present only in the windowsapp config, not in the service config
            string disableVersionCheck = ConfigurationManager.AppSettings["DisableVersionCheck"];
            if (string.IsNullOrEmpty(disableVersionCheck))
                disableVersionCheck = "false";

            #endregion

            #region Application setting configuration

            var applicationXmlNode = XmlUtilities.StringToXmlNode(LoadXml(@"\Library\Logistics\L_01\application_setting.xml"));
            appSettings.ApplicationID = ValidationUtilities.ParseInt(XmlUtilities.SafeSelect(applicationXmlNode, "IC_ID").InnerText);
            appSettings.ApplicationName = appName;
            appSettings.AsmUsername = XmlUtilities.SafeSelect(applicationXmlNode, "ASM_USERNAME").InnerText;
            appSettings.AsmPassword = XmlUtilities.SafeSelect(applicationXmlNode, "ASM_PASSWORD").InnerText;
            appSettings.IntegrationDataDsn = XmlUtilities.SafeSelect(applicationXmlNode, "CI_CENTRAL_DSN").InnerText;
            appSettings.IsCertAuthEnabled = ValidationUtilities.ParseDbBool(XmlUtilities.SafeSelect(applicationXmlNode, "IS_CERT_AUTH_ENABLED").InnerText);
            appSettings.ConditionValidatorTypename = XmlUtilities.SafeSelect(applicationXmlNode, "CONDITION_VALIDATOR_TYPENAME").InnerText;
            appSettings.CoreServiceLocator = XmlUtilities.SafeSelect(applicationXmlNode, "ASM_SERVICE_LOCATOR").InnerText;
            appSettings.TempFolderPath = "";
            //FileUtilities.ResolveSpecialFolderPath(
            //    ValidationUtilities.ParseString(reader, "TEMP_SPECIAL_FOLDER", true),
            //    ValidationUtilities.ParseString(reader, "TEMP_FOLDER_PATH"));
            appSettings.CommunicationLogServiceCache = XmlUtilities.SafeSelect(applicationXmlNode, "COMM_LOG_SERVICE_CACHE").InnerText;
            appSettings.RequiredCI_CENTRALSchemaVersion = requiredCI_CENTRALSchemaVersion;
            appSettings.RequiredCI_BUSINESSDATASchemaVersion = requiredCI_BUSINESSDATASchemaVersion;
            appSettings.CoreVersionCompatibilityFileName = coreVersionCompatibilityFileName;
            appSettings.DisableVersionCheck = ValidationUtilities.ParseBool(disableVersionCheck);

            #endregion

            return appSettings;

        }

        private static string LoadXml(string filePath)
        {
            try
            {
                var result = File.ReadAllText(filePath);
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
