using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logistic.Integration.Common
{
    public class Configuration
    {
        #region Private Fields

        private static readonly object generalConfigLocker;
        private static readonly object locker;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the <see cref="Configuration"/> class.
        /// </summary>
        static Configuration()
        {
            locker = new object();
            generalConfigLocker = new object();
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
                        appSettings = (ApplicationSettings) Cache.Get("AppSettings");
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
                        (Dictionary<string, Dictionary<string, string>>) Cache.Get("GeneralConfiguration");
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
        }

        #endregion

        private static Dictionary<string, Dictionary<string, string>> LoadGeneralConfiguration()
        {
            return new Dictionary<string, Dictionary<string, string>>();
        }

        private static ApplicationSettings LoadAppSettings()
        {
            return new ApplicationSettings();
        }
    }
}
