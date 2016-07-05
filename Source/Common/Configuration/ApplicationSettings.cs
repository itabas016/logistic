using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logistic.Integration.Common
{
    /// <summary>
    /// This class represents the standard application settings of the integration component.
    /// </summary>
    [Serializable]
    public class ApplicationSettings
    {
        private int applicationID;
        private string applicationName;
        private string asmPassword;
        private string asmUsername;
        private string conditionValidatorTypename;
        private string integrationDataDsn;
        private bool isCertAuthEnabled;
        private string tempFolderPath;
        private bool disableVersionCheck;
        private string coreServiceLocator;
        private string requiredCI_CENTRALSchemaVersion;
        private string requiredCI_BUSINESSDATASchemaVersion;
        private string coreVersionCompatibilityFileName;
        private string communicationLogServiceCache;

        public string CommunicationLogServiceCache
        {
            get { return communicationLogServiceCache; }
            set { communicationLogServiceCache = value; }
        }

        public string CoreServiceLocator
        {
            get { return coreServiceLocator; }
            set { coreServiceLocator = value; }
        }

        public string ApplicationName
        {
            get { return applicationName; }
            set { applicationName = value; }
        }

        public int ApplicationID
        {
            get { return applicationID; }
            set { applicationID = value; }
        }

        public bool IsCertAuthEnabled
        {
            get { return isCertAuthEnabled; }
            set { isCertAuthEnabled = value; }
        }

        public string AsmUsername
        {
            get { return asmUsername; }
            set { asmUsername = value; }
        }

        public string AsmPassword
        {
            get { return asmPassword; }
            set { asmPassword = value; }
        }

        public string IntegrationDataDsn
        {
            get { return integrationDataDsn; }
            set { integrationDataDsn = value; }
        }

        public string ConditionValidatorTypename
        {
            get { return conditionValidatorTypename; }
            set { conditionValidatorTypename = value; }
        }

        public string TempFolderPath
        {
            get { return tempFolderPath; }
            set { tempFolderPath = value; }
        }

        public string RequiredCI_CENTRALSchemaVersion
        {
            get { return requiredCI_CENTRALSchemaVersion; }
            set { requiredCI_CENTRALSchemaVersion = value; }
        }

        public string RequiredCI_BUSINESSDATASchemaVersion
        {
            get { return requiredCI_BUSINESSDATASchemaVersion; }
            set { requiredCI_BUSINESSDATASchemaVersion = value; }
        }

        public string CoreVersionCompatibilityFileName
        {
            get { return coreVersionCompatibilityFileName; }
            set { coreVersionCompatibilityFileName = value; }
        }

        public bool DisableVersionCheck
        {
            get { return disableVersionCheck; }
            set { disableVersionCheck = value; }
        }
    }
}
