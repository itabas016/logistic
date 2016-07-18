using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PayMedia.Integration.IFComponents.BBCL.Logistics;
using NUnit.Framework;
using PayMedia.Integration.FrameworkService.Common;
using PayMedia.Integration.FrameworkService.Interfaces.Common;
using Rhino.Mocks;
using Rhino.Mocks.Utilities;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics.Tests.Common
{
    [TestFixture]
    public class UnitTester
    {
        public IComponentInitContext _componentInitContext;
        public UnitTester()
        {
            Configuration.ClearConfiguration();

            _componentInitContext = MockRepository.GenerateMock<IComponentInitContext>();
            var workerSettingString = GetFileResource(@"\L_01\worker_setting.xml");
            var applicationSetting = GetFileResource(@"\L_01\application_setting.xml");
            var generalSetting = GetFileResource(@"\L_01\general_setting.xml");

            var configuration = PropertySet.Create();
            configuration[Const.PROP_WORKER_SETTING] = workerSettingString;
            configuration[Const.PROP_APPLICATION_SETTING] = applicationSetting;
            configuration[Const.PROP_GENERAL_SETTING] = generalSetting;

            _componentInitContext.Stub(s => s.Config).Return((IReadOnlyPropertySet)configuration);

            Configuration.Init(_componentInitContext);
        }

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
            var workerSettingXmlNode = Configuration.WorkerSetting;

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

        private static string GetFileResource(string filePath)
        {
            var fileDirectoryPrefix = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(UnitTester)).Location);

            var result = File.ReadAllText(string.Format(@"{0}..\{1}", fileDirectoryPrefix, filePath));
            return result;
        }
        #endregion

        #region File watcher



        #endregion

        #region File process



        #endregion
    }
}
