using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PayMedia.ApplicationServices.CustomFields.ServiceContracts;
using PayMedia.ApplicationServices.CustomFields.ServiceContracts.DataContracts;
using PayMedia.ApplicationServices.Devices.ServiceContracts;
using PayMedia.ApplicationServices.Logistics.ServiceContracts;
using PayMedia.ApplicationServices.Logistics.ServiceContracts.DataContracts;
using PayMedia.ApplicationServices.ProductCatalog.ServiceContracts;
using PayMedia.ApplicationServices.ProductCatalog.ServiceContracts.DataContracts;
using PayMedia.ApplicationServices.SharedContracts;
using LookupLists = PayMedia.ApplicationServices.Devices.ServiceContracts.LookupLists;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public class L_01_Utils
    {
        #region Private properties

        private IDevicesConfigurationService svcDevicesConfiguration;
        private ICustomFieldsConfigurationService svcCustomFieldsConfigurationService;
        private ICustomFieldsService svcCustomFieldsService;
        private ILogisticsService svcLogisticsService;
        private IProductCatalogConfigurationService svcProductCatalogConfig;

        private Dictionary<string, int> StockHandlers = new Dictionary<string, int>();
        private Dictionary<string, int> EventReasons = new Dictionary<string, int>();
        private Dictionary<string, int> HardwareModels = new Dictionary<string, int>();
        private Dictionary<string, int> DeviceCustomFields = new Dictionary<string, int>();
        private Dictionary<int, string> UpdateDeviceReasons = new Dictionary<int, string>();
        private string externalStockHandlerID;

        private int errorLoadingCache = 0; // 0 = no errors, 1 = error.

        #endregion

        #region Ctor

        public L_01_Utils(string dsn, string externalStockHandlerID)
        {
            svcDevicesConfiguration = ServiceUtilities.GetService<IDevicesConfigurationService>(dsn);
            svcCustomFieldsService = ServiceUtilities.GetService<ICustomFieldsService>(dsn);
            svcCustomFieldsConfigurationService = ServiceUtilities.GetService<ICustomFieldsConfigurationService>(dsn);
            svcLogisticsService = ServiceUtilities.GetService<ILogisticsService>(dsn);
            svcProductCatalogConfig = ServiceUtilities.GetService<IProductCatalogConfigurationService>(dsn);

            // lets load all the caches at the same time, each on their own thread.
            List<Thread> cacheThreads = new List<Thread>();

            this.externalStockHandlerID = externalStockHandlerID; // CacheStockHandlerIDs() needs this set before it executes.

            cacheThreads.Add(new Thread(new ThreadStart(CacheStockHandlerIDs)));
            cacheThreads.Add(new Thread(new ThreadStart(CacheDeviceCustomFields)));
            cacheThreads.Add(new Thread(new ThreadStart(CacheEventReasons)));
            cacheThreads.Add(new Thread(new ThreadStart(CacheModels)));
            cacheThreads.Add(new Thread(new ThreadStart(CacheUpdateDeviceReasons)));

            foreach (Thread thread in cacheThreads)
            {
                thread.IsBackground = true;
                thread.Start();
            }

            foreach (Thread thread in cacheThreads)
                thread.Join();

            // now lets see how our caching calls went...
            if (this.errorLoadingCache == 1)
                throw new IntegrationException("An error occurred while loading our cache.  Please see previous error mesages for more details.");

            if (StockHandlers.Count == 0)
                throw new IntegrationException(string.Format("NO Stock Handlers were found with CustomField '{0}'", externalStockHandlerID));

            if (EventReasons.Count == 0)
            {
                string error = string.Format("No EventReasons were found for Events {0}, {1}, {2}, {3}",
                    LookupLists.NewStockReceiveReasons.ToString(),
                    LookupLists.PairDevicesReasons.ToString(),
                    LookupLists.TransferDeviceAtStockHandlerReason.ToString(),
                    LookupLists.ChangeDeviceStatusReasons.ToString());
                throw new IntegrationException(error);
            }

            if (HardwareModels.Count == 0)
            {
                throw new IntegrationException("NO Hardware Models were returned from IBS Core!");
            }

            if (UpdateDeviceReasons.Count == 0)
            {
                string error = string.Format("No UpdateDeviceReasons were found for UpdateDeviceReasons {0}",
                    PayMedia.ApplicationServices.Devices.ServiceContracts.LookupLists.UpdateDeviceReasons.ToString());
                throw new IntegrationException(error);
            }
        }

        #endregion

        #region Public Methods

        public int GetStockHandlerID(string locationID, string externalStockHandler)
        {
            externalStockHandlerID = externalStockHandler;
            int stockHandlerID;
            StockHandlers.TryGetValue(locationID, out stockHandlerID);
            if (stockHandlerID == 0)
            {
                Diagnostics.Info(string.Format("EC_8 LocationID {0} is not a valid StockHandler in IBS", locationID));
            }
            return stockHandlerID;
        }

        public int GetReasonID(string eventNumber, string description)
        {
            int reasonID = 0;
            string key = eventNumber + "-" + description;
            EventReasons.TryGetValue(key, out reasonID);
            if (reasonID == 0)
            {
                Diagnostics.Info(string.Format("EC_7 Reason {0} not defined in IBS", description));
            }

            return reasonID;
        }

        public int GetHardwareModelID(string modelName)
        {
            int hardwareModelID;
            HardwareModels.TryGetValue(modelName, out hardwareModelID);
            if (hardwareModelID == 0)
            {
                Diagnostics.Info(string.Format("Hardware model {0} not found", modelName));
            }
            return hardwareModelID;
        }

        public bool DeviceCustomFieldExists(string customField, out int customFieldId)
        {
            return DeviceCustomFields.TryGetValue(customField, out customFieldId);
        }

        public bool UpdateDeviceReasonExists(int reason)
        {
            string output;
            return UpdateDeviceReasons.TryGetValue(reason, out output);
        }

        #endregion

        #region Private Methods

        private void CacheStockHandlerIDs()
        {
            try
            {
                string logMessage = "Stock Handlers" + "\r\n";

                StockHandlerCollection stockHandlersCollection;

                int i = 0;
                do
                {
                    stockHandlersCollection = svcLogisticsService.GetStockHandlers(i);

                    foreach (StockHandler stockHandler in stockHandlersCollection.Items)
                    {
                        CustomFieldValueCollection customFieldsCollection = svcCustomFieldsService.GetCustomFieldValues(stockHandler.Id.Value, (int)PayMedia.ApplicationServices.SharedContracts.Entities.Customer);

                        foreach (CustomFieldValue customFieldValue in customFieldsCollection)
                        {
                            if (customFieldValue.Name == externalStockHandlerID)
                            {

                                if (StockHandlers.ContainsKey(customFieldValue.Value))
                                {
                                    //throw new IntegrationException(string.Format("Duplicate {0} CustomField value found for customer id {1}.", customFieldValue.Value, stockHandler.Id));
                                }
                                else
                                {
                                    StockHandlers.Add(customFieldValue.Value, stockHandler.Id.Value);
                                    logMessage += customFieldValue.Value + "---" + stockHandler.Id.Value.ToString() + "\r\n";
                                }
                            }
                        }
                    }
                    i++;
                } while (stockHandlersCollection.More == true);  //Checking current page before going on to next.

                Diagnostics.Info(logMessage);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref this.errorLoadingCache, 1);
                Diagnostics.Error(string.Format("Error occured while loading cache in {0}.\r\n{1}", MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
        }

        private void CacheDeviceCustomFields()
        {
            try
            {
                string logMessage = "Device Custom Fields" + "\r\n";

                string[] attributeValue = null;
                CustomFieldPerEntityCollection customFieldsCollection = svcCustomFieldsConfigurationService.RequestCustomFieldsPerEntity(new CustomFieldPerEntityCriteria()
                {
                    EntityId = (int)PayMedia.ApplicationServices.SharedContracts.Entities.Device
                });
                //CustomFieldPerEntityCollection customFieldsCollection = svcCustomFieldsConfigurationService.GetCustomFieldsPerEntity( (int) PayMedia.ApplicationServices.SharedContracts.Entities.Device, attributeValue, 0 );
                foreach (CustomFieldPerEntity customFieldPerEntity in customFieldsCollection.Items)
                {
                    if (DeviceCustomFields.ContainsKey(customFieldPerEntity.CustomField.Name))
                    {
                        //throw new IntegrationException(string.Format("Duplicate {0} CustomField found for Device entity.", customFieldPerEntity.CustomField.Name));
                    }
                    else
                    {
                        DeviceCustomFields.Add(customFieldPerEntity.CustomField.Name, (int)customFieldPerEntity.CustomField.Id);
                        logMessage += customFieldPerEntity.CustomField.Id + "---" + customFieldPerEntity.CustomField.Name + "\r\n";
                    }
                }

                Diagnostics.Info(logMessage);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref this.errorLoadingCache, 1);
                Diagnostics.Error(string.Format("Error occured while loading cache in {0}.\r\n{1}", MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
        }

        private void CacheUpdateDeviceReasons()
        {
            try
            {
                string logMessage = "UpdateDeviceReasons \r\n";
                LookupCollection lookupCollection =
                    svcDevicesConfiguration.GetLookups(LookupLists.UpdateDeviceReasons);

                foreach (Lookup lookup in lookupCollection)
                {
                    UpdateDeviceReasons.Add(Convert.ToInt32(lookup.Key), lookup.Description);
                    logMessage += lookup.Key + "---" + lookup.Description + "\r\n";
                }
                Diagnostics.Info(logMessage);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref this.errorLoadingCache, 1);
                Diagnostics.Error(string.Format("Error occured while loading cache in {0}.\r\n{1}", MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
        }

        private void CacheReasons(LookupLists lookupList)
        {
            try
            {
                string logMessage = "Event " + ((int)lookupList).ToString() + "\r\n";
                LookupCollection lookupCollection = svcDevicesConfiguration.GetLookups(lookupList);
                foreach (Lookup lookup in lookupCollection)
                {
                    EventReasons.Add(((int)lookupList).ToString() + "-" + lookup.Description, Convert.ToInt32(lookup.Key));
                    logMessage += ((int)lookupList).ToString() + "-" + lookup.Description + "---" + lookup.Key + "\r\n";
                }
                Diagnostics.Info(logMessage);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref this.errorLoadingCache, 1);
                Diagnostics.Error(string.Format("Error occured while loading cache in {0}.\r\n{1}", MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
        }

        private void CacheEventReasons()
        {
            try
            {
                // event 749
                CacheReasons(LookupLists.NewStockReceiveReasons);
                // event 280
                CacheReasons(LookupLists.PairDevicesReasons);
                // event 183
                CacheReasons(LookupLists.TransferDeviceAtStockHandlerReason);
                // event 156
                CacheReasons(LookupLists.ChangeDeviceStatusReasons);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref this.errorLoadingCache, 1);
                Diagnostics.Error(string.Format("Error occured while loading cache in {0}.\r\n{1}", MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
        }

        private void CacheModels()
        {
            try
            {
                string logMessage = "Hardware models" + "\r\n";
                HardwareModelCollection models;

                int i = 1;
                do
                {
                    models = svcProductCatalogConfig.GetHardwareModels(i);

                    foreach (HardwareModel item in models.Items)
                    {
                        if (!HardwareModels.ContainsKey(item.Description))
                        {
                            HardwareModels.Add(item.Description, item.Id.Value);
                            logMessage += item.Description + "---" + item.Id.Value.ToString() + "\r\n";
                        }
                    }
                    i++;
                } while (models.More == true);  //Checking current page before going on to next.
                Diagnostics.Info(logMessage);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref this.errorLoadingCache, 1);
                Diagnostics.Error(string.Format("Error occured while loading cache in {0}.\r\n{1}", MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
        }

        #endregion
    }
}
