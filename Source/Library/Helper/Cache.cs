using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
	/// Cache
	/// </summary>
	[Serializable()]
    public static class Cache
    {
        #region Static Members
        private static Dictionary<string, object> storage = new Dictionary<string, object>();
        private static object _synchronizationObjectStatic = new object();
        #endregion

        #region static Methods
        /// <summary>
        /// Clears this instance.
        /// </summary>
        /// <returns></returns>
        public static bool Clear()
        {
            if (storage.Count != 0)
            {
                lock (_synchronizationObjectStatic)
                {
                    storage.Clear();
                }
            }
            return true;
        }
        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        public static int Count
        {
            get
            {
                return storage.Count;
            }
        }
        /// <summary>
        /// Adds the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public static void Add(string key, object value)
        {
            if (!storage.ContainsKey(key))
            {
                lock (_synchronizationObjectStatic)
                {
                    if (!storage.ContainsKey(key))
                    {
                        storage.Add(key, value);
                    }
                }
            }
            else
            {
                throw new IntegrationException(string.Format("Key '{0}' already exists with value '{1}'", key, value));
            }
        }
        /// <summary>
        /// Determines whether the specified key contains key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// 	<c>true</c> if the specified key contains key; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsKey(string key)
        {
            lock (_synchronizationObjectStatic)
            {
                return storage.ContainsKey(key);
            }
        }
        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public static object Get(string key)
        {
            lock (_synchronizationObjectStatic)
            {
                return (object)storage[key];
            }
        }
        /// <summary>
        /// Removes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public static void Remove(string key)
        {
            if (storage.ContainsKey(key))
            {
                lock (_synchronizationObjectStatic)
                {
                    if (storage.ContainsKey(key))
                    {
                        storage.Remove(key);
                    }
                }
            }

            else
            {
                //Do nothing if the key was not found.
            }
        }
        /// <summary>
        /// Replace existing or add a new key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public static void Replace(string key, object value)
        {
            lock (_synchronizationObjectStatic)
            {
                // if our key exists, remove it.
                if (storage.ContainsKey(key))
                {
                    storage.Remove(key);
                }
                // no add new key.
                storage.Add(key, value);
            }

        }
        #endregion
    }
}
