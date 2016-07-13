using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PayMedia.Integration.IFComponents.BBCL.Logistics;
using NUnit.Framework;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics.Tests.Common
{
    [TestFixture]
    public class UnitTester
    {
        #region Configuration

        [Test]
        public void get_application_setting_test()
        {
            var applicationSettings = Configuration.AppSettings;

            Assert.AreEqual(1, applicationSettings.ApplicationID);
            Assert.AreEqual(false, applicationSettings.IsCertAuthEnabled);
            Assert.AreEqual("http://localhost/asm/all/servicelocation.svc", applicationSettings.CoreServiceLocator);
        }

        [Test]
        public void get_worker_setting_test()
        {
            var workerSettingXmlNode = Configuration.GetWorkerConfiguration();

            var message_name = XmlUtilities.SafeSelect(workerSettingXmlNode, "MessageName").InnerText;
            var ftp_url = XmlUtilities.SafeSelect(workerSettingXmlNode, "ftpUrl").InnerText;

            Assert.AreEqual("L_01_UploadDevicesAndPairing", message_name);
            Assert.AreEqual("ftp://192.168.193.179/SAP/Logistics/L_01/", ftp_url);

        }

        [Test]
        public void get_general_configuration_setting_test()
        {
            var testGroup = "wodFTP.NET";
            var key_ConnectTimeOutSeconds = "ConnectTimeOutSeconds";
            var key_OperationTimeOutSeconds = "OperationTimeOutSeconds";
            var value_ConnectTimeOutSeconds = Configuration.GetGeneralConfigValue(testGroup, key_ConnectTimeOutSeconds);
            var value_OperationTimeOutSeconds = Configuration.GetGeneralConfigValue(testGroup, key_OperationTimeOutSeconds);

            Assert.AreEqual("60", value_ConnectTimeOutSeconds);
            Assert.AreEqual("60", value_OperationTimeOutSeconds);

        }

        #endregion
    }
}
