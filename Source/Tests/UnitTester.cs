using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PayMedia.Integration.IFComponents.BBCL.Logistics;
using NUnit.Framework;
using PayMedia.Integration.FrameworkService.Common;
using PayMedia.Integration.FrameworkService.Interfaces.Common;
using Rhino.Mocks;
using Rhino.Mocks.Utilities;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics.Tests
{
    [TestFixture]
    public class UnitTester
    {
        public IComponentInitContext _componentInitContext;
        public FtpWatcherHelper _ftpWatcher;

        public const string TEST_FILE_NAME = @"L_01_DeviceUpload.2016-07-19.93.new";

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

            // init ftpwatcher instance
            _ftpWatcher = new FtpWatcherHelper(Configuration.FtpWatcherSetting);

        }

        #region Configuration

        [Test]
        public void get_application_setting_test()
        {
            var applicationSettings = Configuration.AppSettings;

            Assert.AreEqual(@"shalab\entriqeng", applicationSettings.AsmUsername);
            Assert.AreEqual("entriqeng", applicationSettings.AsmPassword);
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
            var fileDirectoryPrefix = GetFileParentDirectory();

            var result = File.ReadAllText(string.Format(@"{0}..\{1}", fileDirectoryPrefix, filePath));
            return result;
        }

        private static string GetFileParentDirectory()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(UnitTester)).Location);
        }

        #endregion

        #region File watcher monitor and process

        [Test]
        public void test_file_receive_and_process()
        {
            var expectFileProcessName = @"L_01_DeviceUpload.2016-07-19.93.process";
            var sourcePath = string.Format(@"{0}\L_01\", GetFileParentDirectory());
            var targetPath = Configuration.FtpWatcherSetting.PollingEndpoint.Address;
            var sourceFile = Path.Combine(sourcePath, TEST_FILE_NAME);

            try
            {
                UploadFileToFTP(sourceFile);

                _ftpWatcher.OnFileReceived += FakeExecute;

                _ftpWatcher.RequestStart();

                if (!Configuration.FtpWatcherSetting.DeleteAfterDownloading)
                {
                    Assert.True(!File.Exists(string.Format("{0}{1}", targetPath, TEST_FILE_NAME)));
                }
                Thread.Sleep(10000);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _ftpWatcher.RequestStop(30);
            }
            finally
            {
                //Assert.True(File.Exists(string.Format("{0}{1}", targetPath, expectFileProcessName)));
                Console.WriteLine(string.Format("{0}{1}", targetPath, expectFileProcessName));
                _ftpWatcher.RequestStop(30);
            }
        }

        #endregion

        #region private methods

        private void UploadFileToFTP(string sourcePath)
        {
            using (WebClient client = new WebClient())
            {
                string filename = Path.GetFileName(sourcePath);
                client.Credentials = new NetworkCredential(Configuration.FtpWatcherSetting.PollingEndpoint.Username, Configuration.FtpWatcherSetting.PollingEndpoint.Password);
                client.UploadFile(string.Format("{0}{1}", Configuration.FtpWatcherSetting.PollingEndpoint.Address, filename), "STOR", sourcePath);
            }
        }

        private void FakeExecute(FtpMessageInfo messageInfo)
        {
            var expectPath = string.Format(@"{0}\L_01\", GetFileParentDirectory());
            var expectFile = Path.Combine(expectPath, TEST_FILE_NAME);

            var expectFileInfo = new FileInfo(expectFile);
            var expectStoragePath = string.Format(@"{0}\{1}\{2}", Configuration.FtpWatcherSetting.StorageFilePath, 
                DateTime.Now.ToString("yyyy\\\\MM\\\\dd"), TEST_FILE_NAME);

            Assert.AreEqual(expectFileInfo.Extension.TrimStart('.'), messageInfo.Extension);
            Assert.AreEqual(expectStoragePath, messageInfo.FilePath);

        }

        #endregion
    }
}
