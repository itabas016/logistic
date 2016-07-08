using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logistic.Integration.Common;
using NUnit.Framework;

namespace Logistic.Integration.Tests.Common
{
    [TestFixture]
    public class UnitTester
    {
        #region Configuration

        [Test]
        public void get_application_setting_test()
        {
            var applicationSettings = Configuration.AppSettings;

            Assert.AreEqual("", applicationSettings.ApplicationID);
        }

        #endregion
    }
}
