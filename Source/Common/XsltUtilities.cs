using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace Logistic.Integration.Common
{
    public static class XsltUtilities
    {
        private static ReaderWriterLockSlim transformCacheLock = new ReaderWriterLockSlim();
        private static Dictionary<string, XslCompiledTransform> compliedTransforms = new Dictionary<string, XslCompiledTransform>();

        /// <summary>
        /// All this class attempts to do is return a previously compiled XSL Transform from a Dictionary of compiled XSL Transforms.<para/>
        /// If the XSLT specified in <paramref name="xsltPath"/> is NOT in the Dictionary then it is loaded, compiled, added and returned.
        /// </summary>
        /// <param name="xsltPath">The filepath to the XSLT you want returned from the compiled store.</param>
        /// <returns>A compiled XSL Transform</returns>
        public static XslCompiledTransform GetCompiledTransform(string xsltPath)
        {
            XslCompiledTransform transform = null;

            try
            {
                // note: 2012.01.12 JCopus - Since I expect 99.99% of the time an XSLT will be in the cache I'll start with a read lock.
                //  I am NOT using EnterUpgradableReadLock since only one thread at a time can hold that type of lock.
                //  Instead I will first enter a read lock, and then ONLY if I don't get a hit will I lock, double check that 
                //  another thread didn't beat me to adding it, and add the compiled transform.
                try
                {
                    transformCacheLock.EnterReadLock();

                    compliedTransforms.TryGetValue(xsltPath, out transform);
                }
                finally
                {
                    if (transformCacheLock.IsReadLockHeld == true)
                        transformCacheLock.ExitReadLock();
                }

                if (transform == null)
                {
                    transformCacheLock.EnterWriteLock();

                    compliedTransforms.TryGetValue(xsltPath, out transform);
                    if (transform == null)
                    {
                        transform = CreateCompiledTransform(xsltPath);

                        compliedTransforms.Add(xsltPath, transform);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new IntegrationException(string.Format("Error occured while trying to Retrieve or Create XslCompiledTransform. \r\nError occurred in {0}.{1}():\r\n{2}", MethodBase.GetCurrentMethod().DeclaringType.Name, MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }
            finally
            {
                if (transformCacheLock.IsWriteLockHeld == true)
                    transformCacheLock.ExitWriteLock();
            }

            return transform;
        }

        private static XslCompiledTransform CreateCompiledTransform(string xsltPath)
        {
            try
            {
                XslCompiledTransform transform = new XslCompiledTransform();
                transform.Load(xsltPath, XsltSettings.TrustedXslt, new XmlUrlResolver());
                return transform;
            }
            catch (Exception ex)
            {
                throw new IntegrationException(string.Format("Error occured while trying to Compile the XSL file '{3}'. Please verify the file exists at the location specified and verify it is valid.\r\nError occurred in {0}.{1}():\r\n{2}", MethodBase.GetCurrentMethod().DeclaringType.Name, MethodBase.GetCurrentMethod().Name, ex.ToString(), xsltPath));
            }
        }
    }
}
