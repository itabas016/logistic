using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PayMedia.ApplicationServices.ClientProxy;
using PayMedia.ApplicationServices.ClientProxy.SpecialDecorators;
using PayMedia.ApplicationServices.SharedContracts;
using PayMedia.Integration.CommunicationLog.ServiceContracts;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
    /// Utilities for dealing with PayMedia ApplicationServices.
    /// </summary>
    public static class ServiceUtilities
    {
        #region Private Fields
        [ThreadStatic]
        private static string contextUsername;

        [ThreadStatic]
        private static string contextPassword;

        private static ReaderWriterLockSlim commLogServiceSlimLock = new ReaderWriterLockSlim();
        // we keep a copy of the CommunicationLogServiceHandle whan we use core caching of this proxy 
        // because we MUST explicitly dispose of it so that it will flush any cached log entries to core.
        private static ICommunicationLogService CommunicationLogServiceHandle = null;

        #endregion

        /// <summary>
        /// The Context username.
        /// </summary>
        public static string ContextUsername
        {
            set { contextUsername = value; }
            get { return contextUsername; }
        }

        /// <summary>
        /// The Context username.
        /// </summary>
        public static string ContextPassword
        {
            set { contextPassword = value; }
            get { return contextPassword; }
        }

        static ServiceUtilities()
        {
            string AsmServiceLocatorUrl = Configuration.AppSettings.CoreServiceLocator;
            bool useAsmWeakReferenceCaching = false;
            int wcfProxiesPerUser = 0;

            // Attempt to read WCFProxiesPerUser configuration setting
            //if (!int.TryParse(ConfigurationManager.AppSettings["WCFProxiesPerUser"], out wcfProxiesPerUser))
                //throw new IntegrationException("Unable to read setting \"WCFProxiesPerUser\" from the IC config file.  Please verify this setting is present and valid.");

            // 2010.12.03 JCopus - At the moment weak references do NOT really work in the core client proxy and I'm not going
            // to spend time added the extra configuraiton field to IC main to support it.  Once we whould add it in the hopes
            // Core client caching will use it.  So for now we ALWAYS you false for useAsmWeakReferenceCaching.
            //// note: any configuration value, or lack there of, that is not explictly true will quietly evaluate to false.
            ////bool.TryParse( ConfigurationManager.AppSettings[ "UseAsmWeakReferenceCaching" ], out useAsmWeakReferenceCaching );

            AsmRepository.SetServiceLocationUrl(AsmServiceLocatorUrl);
            AsmRepository.UseWeakReferences = useAsmWeakReferenceCaching;
            AsmRepository.WCFProxiesPerUser = wcfProxiesPerUser;
        }

        /// <summary>
		/// Instantiate a service using the context's DSN.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T GetService<T>()
        {
            T service = default(T);

            string username = Configuration.AppSettings.AsmUsername;
            string proof = Configuration.AppSettings.AsmPassword;
            string dsn = Configuration.AppSettings.IntegrationDataDsn;

            service = ServiceUtilities.GetService<T>(username, proof, dsn);

            // Return the service.
            return service;
        }

        /// <summary>
        /// Get a service using the supplied DSN and the application's authentication.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dsn"></param>
        /// <returns></returns>
        public static T GetService<T>(string dsn)
        {
            // Retrieve the credentials.
            string proof, username;
            if (!String.IsNullOrEmpty(ContextPassword))
            {
                proof = ContextPassword;
            }
            else
            {
                proof = Configuration.AppSettings.AsmPassword;
            }

            if (!String.IsNullOrEmpty(ContextUsername))
            {
                username = ContextUsername;
            }
            else
            {
                username = Configuration.AppSettings.AsmUsername;
            }

            // Return the service.
            return GetService<T>(username, proof, dsn);
        }

        public static T GetService<T>(string username, string proof, string dsn)
        {
            T service = default(T);

            // Create the AuthenticationHeader.
            AuthenticationHeader authentication = new AuthenticationHeader();

            // Set the credentials.
            authentication.UserName = username;
            authentication.Proof = proof;
            authentication.Dsn = dsn;

            // Instantiate the service.
            service = UpdateCache<T>(authentication);

            // Return the service.
            return service;
        }

        private static T UpdateCache<T>(AuthenticationHeader authentication)
        {
            T service = default(T);

            // if this is a ICommunicationLogService then we treat it very special because it caches
            // its writes and we need to keep a handle around so that when we close the application we 
            // displose of the proxy, which causes it to flush any pending writes.
            // Yet Another Worthwile Note (YAWN): Per Anders, he only caches one instance of the cached
            //    comm log service, ignoring user authentication.  So for the comm log service we do
            //    NOT create one unique instance per user.  They all share the first one created.

            try
            {
                commLogServiceSlimLock.EnterUpgradeableReadLock();
                if (CommunicationLogServiceHandle == null && typeof(T).Name == "ICommunicationLogService")
                {
                    commLogServiceSlimLock.EnterWriteLock();

                    int maxEnteries = 500;
                    int secondsToCache = 5;

                    // try and read our comm log cach parameter from the application file.
                    // if they are NOT there or there was any problems, log a warning and use our default values.
                    try
                    {
                        string rawSetting = Configuration.AppSettings.CommunicationLogServiceCache;

                        if (rawSetting != null)
                        {
                            int[] cacheSettings = Array.ConvertAll<string, int>(rawSetting.Split(','), Convert.ToInt32);
                            if (cacheSettings.Length == 2)
                            {
                                if (cacheSettings[0] > 5000)
                                {
                                    string warning = string.Format("Warning the maximum value for the CommunicationLogServiceCache '<max cached entries>' setting is 5000.  The value configure was '{0}'.\r\nThe default settings of {1} '<max cached entries>' and {2} '<cache timeout in seconds>' will be used.", cacheSettings[0], maxEnteries, secondsToCache);
                                    Diagnostics.Warning(warning);
                                }
                                else if (cacheSettings[1] > 600)
                                {
                                    string warning = string.Format("Warning the maximum value for the CommunicationLogServiceCache '<cache timeout in seconds>' setting is 600 (ten minutes).  The value configure was '{0}'.\r\nThe default settings of {1} '<max cached entries>' and {2} '<cache timeout in seconds>' will be used.", cacheSettings[1], maxEnteries, secondsToCache);
                                    Diagnostics.Warning(warning);
                                }
                                else
                                {
                                    maxEnteries = cacheSettings[0];
                                    secondsToCache = cacheSettings[1];
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string warning = string.Format("Warning: There was a problem reading the CommunicationLogServiceCache setting from the application configuration file.\r\nThe default settings of {0} '<max cached entries>' and {1} '<cache timeout in seconds>' will be used.\r\nThe error encountered was: \r\n{2}", maxEnteries, secondsToCache, ex.ToString());
                        Diagnostics.Warning(warning);
                    }

                    var realCommlogService = AsmRepository.GetServiceProxy<ICommunicationLogService>(authentication);

                    // Core may throw the following error:
                    // System.ArgumentException: An item with the same key has already been added.
                    // Or perhaps another error.  This will catch any error, and log it.
                    // This at least gives us the chance to attempt to continue.
                    try
                    {
                        AsmRepository.AddServiceDecorator<ICommunicationLogService>(new CommunicationLogService(realCommlogService, maxEnteries, secondsToCache));
                    }
                    catch (Exception ex)
                    {
                        //Logging.IcLogger.WriteError(string.Format("An error occurred while attempting to set up the Communication Log Service.  Please report this error to Irdeto.\r\n{0}", ex));
                        Diagnostics.Error(string.Format("An error occurred while attempting to set up the Communication Log Service.  Please report this error to Irdeto.\r\n{0}", ex));
                    }

                    CommunicationLogServiceHandle = AsmRepository.GetServiceProxy<ICommunicationLogService>(authentication);
                    commLogServiceSlimLock.ExitWriteLock();
                    service = (T)CommunicationLogServiceHandle;
                }
                else
                {
                    commLogServiceSlimLock.ExitUpgradeableReadLock();
                    service = AsmRepository.GetServiceProxy<T>(authentication);
                }
            }
            // NOTE: NO catch here.  Any exceptions will be thrown back to the caller.
            finally
            {
                if (commLogServiceSlimLock.IsWriteLockHeld == true)
                    commLogServiceSlimLock.ExitWriteLock();

                if (commLogServiceSlimLock.IsUpgradeableReadLockHeld == true)
                    commLogServiceSlimLock.ExitUpgradeableReadLock();
            }

            return service;
        }

    }
}
