using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PayMedia.ApplicationServices.CustomFields.ServiceContracts;
using PayMedia.ApplicationServices.CustomFields.ServiceContracts.DataContracts;
using PayMedia.ApplicationServices.SharedContracts;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public static class CustomFieldUtilities
    {
        /// <summary>
        /// Get Custom Field Values
        /// </summary>
        /// <param name="GetShippingOrderType "></param>
        /// <returns>CustomFieldValueCollection</returns>
        public static CustomFieldValueCollection GetCustomFieldValues(int instanceId, EntityId entityId)
        {
            ICustomFieldsService service = ServiceUtilities.GetService<ICustomFieldsService>();
            CustomFieldValueCollection customFieldValueCollection = service.GetCustomFieldValues(instanceId, (int)entityId);
            return customFieldValueCollection;
        }

        /// <summary>
        /// Create the custome fields collection on the specified entity.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="entityId"></param>
        /// <param name="customFieldValueCollection"></param>
        /// <returns>CustomFieldValueCollection</returns>
        public static CustomFieldValueCollection CreateCustomFieldValues(int instanceId, int entityId, CustomFieldValueCollection customFieldValueCollection)
        {
            ICustomFieldsService service = ServiceUtilities.GetService<ICustomFieldsService>();
            CustomFieldValueCollection returnCustomFieldValueCollection = service.CreateCustomFieldValues(instanceId, entityId, customFieldValueCollection);
            return returnCustomFieldValueCollection;
        }

        /// <summary>
        /// Update the custom fields collection on the specified entity.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="entityId"></param>
        /// <param name="customFieldValueCollection"></param>
        /// <returns>CustomFieldValueCollection</returns>
        public static CustomFieldValueCollection UpdateCustomFieldValues(int instanceId, int entityId, CustomFieldValueCollection customFieldValueCollection)
        {
            ICustomFieldsService service = ServiceUtilities.GetService<ICustomFieldsService>();
            CustomFieldValueCollection returnCustomFieldValueCollection = service.UpdateCustomFieldValues(instanceId, entityId, customFieldValueCollection);
            return returnCustomFieldValueCollection;
        }

        /// <summary>
        /// Get the custom field associated with an entity.
        /// </summary>
        /// <param name="entityId">The entity id to query.</param>
        /// <returns>A CustomFieldPerEntityCollection instance.</returns>
        public static CustomFieldPerEntityCollection GetCustomFieldsPerEntity(int entityId)
        {
            ICustomFieldsConfigurationService service = ServiceUtilities.GetService<ICustomFieldsConfigurationService>();
            CustomFieldPerEntityCollection result = service.RequestCustomFieldsPerEntity(new CustomFieldPerEntityCriteria() { EntityId = entityId });
            return result;
        }
    }
}
