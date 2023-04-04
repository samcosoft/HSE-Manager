using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

using QlmLicenseLib;


namespace QLM;

public class LicenseValidator
{
    #region Data Members

    protected QlmLicense license;
    protected string activationKey;
    protected string computerKey;

    protected bool isEvaluation;
    protected bool evaluationExpired;
    protected int evaluationRemainingDays = -1;
    protected bool wrongProductVersion;

    protected string customData1 = string.Empty;
    protected string customData2 = string.Empty;
    protected string customData3 = string.Empty;

    protected string serverMessage = string.Empty;
    protected EServerErrorCode serverErrorCode = EServerErrorCode.NoError;
    protected ILicenseInfo serverLicenseInfo = new LicenseInfo();

    // You should customize the Product Properties filename as well as the folder where this file is stored.
    // Check the ReadProductPropertiesFile and WriteProductPropertiesFile methods in this class
    protected string productPropertiesFileName = "Samco HSE Management 2.0.lw.xml";

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor initializes the license product definition
    /// </summary>
    public LicenseValidator()
    {
        license = new QlmLicense();

        // Always obfuscate your code. In particular, you should always obfuscate all arguments
        // of DefineProduct and the Public Key (i.e. encrypt all the string arguments).

        // If you are using the QLM License Wizard, you can load the product definition from the settings.xml file generated
        // by the Protect Your App Wizard.
        // To load the settings from the XML file, call the license.LoadSettings function.

        license.DefineProduct (2, "Samco HSE Management", 2, 0, "", "{0c881c24-4ecb-4258-9591-975bd6288e75}");
        license.LicenseEngineLibrary = ELicenseEngineLibrary.DotNet;
        license.PublicKey = "SChPSMloTArWqg==";
        license.RsaPublicKey = "<RSAKeyValue><Modulus>5bzcxFIx0PYiP7sZWH+ZkXUbSXDqH+6qnb80VZHsAWwoYMf2Ueg3NjjtNfH+q6kQdEGwYf/Wa7FcZHqVe0LTvBRBynH2yGdWvLtH6U4+O237ZMUjVIiDnzRQAFSitnek/83gRrORXWPPLcfUMZmAhXMF1Z6ezWg5G8nMyrzyfjk=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        license.CommunicationEncryptionKey = "{12561125-e2b0-429a-b1ce-89c59d746604}";
        license.DefaultWebServiceUrl = "https://demo.samcosoft.ir/qlm/qlmservice.asmx";
        license.StoreKeysLocation = EStoreKeysTo.EFile;
        license.StoreKeysOptions = EStoreKeysOptions.EStoreKeysPerMachine;
        license.ValidateOnServer = false;
        license.PublishAnalytics = true;
        license.EvaluationLicenseKey = "EKRN0-Q0R00-G1EH1-J8M5P-2D2BB-XQWEI-6V2UFJ9";
        license.EvaluationPerUser = true;
        license.EnableMultibyte = true;
        license.ExpiryDateRoundHoursUp = true;
        license.EnableSoapExtension = true;
        license.EnableClientLanguageDetection = true;
        license.LimitTerminalServerInstances = false;
        license.MaxDaysOffline = -1;
        license.MaxDaysOfflineTimerEnabled = false;

        // If you are using QLM Professional, you should also set the communicationEncryptionKey property
        // The CommunicationEncryptionKey must match the value specified in the web.config file of the QLM License Server

        // Make sure that the StoreKeysLocation specified here is consistent with the one specified in the QLM .NET Control
        // If you are using the QlmLicenseWizard, you must set the StoreKeysLocation to EStoreKeysTo.ERegistry

        // To ignore server certificate issues, uncomment this line
        ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidator;
    }

    #endregion

    #region Public Methods
    public virtual bool ValidateLicenseAtStartup(ELicenseBinding licenseBinding, ref bool needsActivation, ref string returnMsg)
    {
        return ValidateLicenseAtStartup(string.Empty, licenseBinding, ref needsActivation, ref returnMsg);
    }


    /// <remarks>Call ValidateLicenseAtStartup when your application is launched. 
    /// If this function returns false, exit your application.
    /// </remarks>
    /// 
    /// <summary>
    /// Validates the license when the application starts up. 
    /// The first time a license key is validated successfully,
    /// it is stored in a hidden file on the system. 
    /// When the application is restarted, this code will load the license
    /// key from the hidden file and attempt to validate it again. 
    /// If it validates succesfully, the function returns true.
    /// If the license key is invalid, expired, etc, the function returns false.
    /// </summary>
    /// <param name="computerID">Unique Computer identifier</param>
    /// <param name="returnMsg">Error message returned, in case of an error</param>
    /// <returns>true if the license is OK.</returns>
    public virtual bool ValidateLicenseAtStartup(string computerID, ELicenseBinding licenseBinding, ref bool needsActivation, ref string returnMsg)
    {
        returnMsg = string.Empty;
        needsActivation = false;

        var storedActivationKey = string.Empty;
        var storedComputerKey = string.Empty;
        var serverUpdateRequiresReactivation = false;

        license.ReadKeys(ref storedActivationKey, ref storedComputerKey);

        if (!string.IsNullOrEmpty(storedActivationKey))
        {
            activationKey = storedActivationKey;
        }

        if (!string.IsNullOrEmpty(storedComputerKey))
        {
            computerKey = storedComputerKey;
        }
            
        if (string.IsNullOrEmpty(activationKey) && string.IsNullOrEmpty(computerKey))
        {
            if (string.IsNullOrEmpty(license.EvaluationLicenseKey))
            {
                returnMsg = "No license key was found.";
                return false;
            }

            activationKey = license.EvaluationLicenseKey;
        }

        var ret = ValidateLicense(activationKey, computerKey, ref computerID, licenseBinding, ref needsActivation, ref returnMsg);

        if (ret && license.ValidateOnServer)
        {
            // If the local license is valid, check on the server if it's valid as well.

            // When ValidateLicenseOnServer is not able to contact the License Server:
            //  If MaxDaysOffline is set to -1, ValidateLicenseOnServer will aways return true, connectionSuccessfull will be false.
            //  If MaxDaysOffline is set to a specific value, say 5 days, ValidateLicenseOnServer will return true if no connection was establihed for <= 5 days
            //  otherwise it will return false.              

            // Renitialize the serverLicenseInfo object
            serverLicenseInfo = new LicenseInfo();

            if (license.ValidateLicenseOnServerEx2(string.Empty, activationKey, computerKey, computerID, Environment.MachineName, false, ref serverLicenseInfo, out serverErrorCode, out serverMessage) == false)
            {
                if (serverErrorCode == EServerErrorCode.License_ComputerKeyMismatch)
                {
                    if (ReactivateKey(computerID))
                    {
                        return true;
                    }
                }

                returnMsg = serverMessage;
                return false;
            }

            if (serverErrorCode == EServerErrorCode.NoError)
            {
                serverUpdateRequiresReactivation = serverLicenseInfo.NewExpiryDate != DateTime.MinValue || !string.IsNullOrEmpty(serverLicenseInfo.NewFeatures) || serverLicenseInfo.NewFloatingSeats != -1;
            }
        }

        //
        // If a license has expired but then renewed on the server, reactivating the key will extend the client
        // with the new subscription period.
        //
        if ((wrongProductVersion || EvaluationExpired || serverUpdateRequiresReactivation) && license.ValidateOnServer)
        {
            ret = ReactivateKey(computerID);
        }

        return ret;

    }

    /// <remarks>Call this function in the dialog where the user enters the license key to validate the license.</remarks>
    /// <summary>
    /// Validates a license key. If you provide a computer key, the computer key is validated. 
    /// Otherwise, the activation key is validated. 
    /// If you are using machine bound keys (UserDefined), you can provide the computer identifier, 
    /// otherwise set the computerID to an empty string.
    /// </summary>
    /// <param name="activationKey">Activation Key</param>
    /// <param name="computerKey">Computer Key</param>
    /// <param name="computerID">Unique Computer identifier</param>
    /// <returns>true if the license is OK.</returns>
    public virtual bool ValidateLicense(string activationKey, string computerKey, ref string computerID, ELicenseBinding licenseBinding, ref bool needsActivation, ref string returnMsg)
    {
        var ret = false;

        needsActivation = false;
        isEvaluation = false;
        evaluationExpired = false;
        evaluationRemainingDays = -1;
        wrongProductVersion = false;

        var licenseKey = computerKey;

        if (string.IsNullOrEmpty(licenseKey))
        {
            licenseKey = activationKey;

            if (string.IsNullOrEmpty(licenseKey))
            {
                return false;
            }
        }

        if (licenseBinding == ELicenseBinding.UserDefined)
        {
            returnMsg = license.ValidateLicenseEx(licenseKey, computerID);
        }
        else
        {
            returnMsg = license.ValidateLicenseEx3(licenseKey, licenseBinding, false, false);
            computerID = license.GetComputerID(licenseBinding);
        }

        var nStatus = (int)license.GetStatus();

        if (IsTrue(nStatus, (int)ELicenseStatus.EKeyInvalid) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyProductInvalid) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyMachineInvalid) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyExceededAllowedInstances) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyTampered))
        {
            // the key is invalid
            ret = false;
        }
        else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyVersionInvalid))
        {
            wrongProductVersion = true;
            ret = false;
        }
        else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyDemo))
        {
            isEvaluation = true;

            if (IsTrue(nStatus, (int)ELicenseStatus.EKeyExpired))
            {
                // the key has expired
                ret = false;
                evaluationExpired = true;
            }
            else
            {
                // the demo key is still valid
                ret = true;
                evaluationRemainingDays = license.DaysLeft;
            }
        }
        else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyPermanent))
        {
            // the key is OK                
            ret = true;
        }

        if (ret && license.LicenseType == ELicenseType.Activation)
        {
            needsActivation = true;
            ret = false;
        }

        return ret;

    }

    /// <remarks>Call this function in the dialog where the user enters the license key to validate the license.</remarks>
    /// <summary>
    /// Validates a license key. If you provide a computer key, the computer key is validated. 
    /// Otherwise, the activation key is validated. 
    /// If you are using machine bound keys (UserDefined), you can provide the computer identifier, 
    /// otherwise set the computerID to an empty string.
    /// </summary>
    /// <param name="activationKey">Activation Key</param>
    /// <param name="computerKey">Computer Key</param>
    /// <param name="computerID">Unique Computer identifier</param>
    /// <returns>true if the license is OK.</returns>
    public virtual bool ValidateLicense(string activationKey, string computerKey, string computerID, ref bool needsActivation, ref string returnMsg)
    {
        var ret = false;

        needsActivation = false;
        isEvaluation = false;
        evaluationExpired = false;
        evaluationRemainingDays = -1;
        wrongProductVersion = false;

        var licenseKey = computerKey;

        if (string.IsNullOrEmpty(licenseKey))
        {
            licenseKey = activationKey;

            if (string.IsNullOrEmpty(licenseKey))
            {
                return false;
            }
        }

        returnMsg = license.ValidateLicenseEx(licenseKey, computerID);

        var nStatus = (int)license.GetStatus();

        if (IsTrue(nStatus, (int)ELicenseStatus.EKeyInvalid) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyProductInvalid) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyMachineInvalid) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyExceededAllowedInstances) ||
            IsTrue(nStatus, (int)ELicenseStatus.EKeyTampered))
        {
            // the key is invalid
            ret = false;
        }
        else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyVersionInvalid))
        {
            wrongProductVersion = true;
            ret = false;
        }
        else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyDemo))
        {
            isEvaluation = true;

            if (IsTrue(nStatus, (int)ELicenseStatus.EKeyExpired))
            {
                // the key has expired
                ret = false;
                evaluationExpired = true;
            }
            else
            {
                // the demo key is still valid
                ret = true;
                evaluationRemainingDays = license.DaysLeft;
            }
        }
        else if (IsTrue(nStatus, (int)ELicenseStatus.EKeyPermanent))
        {
            // the key is OK                
            ret = true;
        }

        if (ret)
        {

            if (license.LicenseType == ELicenseType.Activation)
            {
                needsActivation = true;
                ret = false;
            }
        }

        return ret;

    }

    /// <summary>
    /// Delete all license keys stored in the registry or on the file system
    /// </summary>
    public void DeleteAllKeys()
    {
        // the license was revoked, we need to remove the keys on this system.
        var saveLocation = license.StoreKeysLocation;

        try
        {
            // Remove keys stored on the file system
            license.StoreKeysLocation = EStoreKeysTo.EFile;
            license.DeleteKeys();

            // Remove keys stored in the registry
            license.StoreKeysLocation = EStoreKeysTo.ERegistry;
            license.DeleteKeys();

            // Remove keys stored in the common data
            license.StoreKeysLocation = EStoreKeysTo.EFileCommonData;
            license.DeleteKeys();
        }
        catch
        {
            // ignored
        }
        finally
        {
            computerKey = string.Empty;
            // Restore the previous setting
            license.StoreKeysLocation = saveLocation;
        }
    }

    /// <summary>
    /// Reactivates a key - this is typically used to automatically get a subscription extension from the server
    /// </summary>
    /// <param name="computerID"></param>
    /// <param name="newComputerKey"></param>
    /// <returns></returns>
    private bool ReactivateKey(string computerID)
    {
        var ret = false;

        if (license.PingEx (string.Empty, out _, out _) == false)
        {
            // we cannot connect to the server so we cannot do any validation with the server
            return false;
        }

        // try to reactivate the license and see if it still expired
        license.ReactivateLicense(license.DefaultWebServiceUrl, ActivationKey, computerID, out var response);

        // Reinitialize the serverLicenseInfo object
        serverLicenseInfo = new LicenseInfo();
        var message = string.Empty;
        if (license.ParseResults(response, ref serverLicenseInfo, ref message))
        {
            var newComputerKey = serverLicenseInfo.ComputerKey;
            serverErrorCode = serverLicenseInfo.ServerErrorCode;
            serverMessage = string.IsNullOrEmpty(serverLicenseInfo.ErrorMessage) ? serverLicenseInfo.InfoMessage : serverLicenseInfo.ErrorMessage;


            var needsActivation = false;
            var returnMsg = string.Empty;

            ret = ValidateLicense(activationKey, newComputerKey, ref computerID, ELicenseBinding.UserDefined, ref needsActivation, ref returnMsg);

            if (ret)
            {
                // The Computer Key has changed, update the local one
                license.StoreKeys(activationKey, newComputerKey);
            }
        }

        return ret;
    }

    /// <summary>
    /// Deletes the license keys stored on the computer. 
    /// </summary>
    public virtual void DeleteKeys()
    {
        license.DeleteKeys();
        this.computerKey = string.Empty;
    }
        
    #endregion

    #region Properties

    /// <summary>
    /// Returns the registered activation key
    /// </summary>
    public string ActivationKey
    {
        get
        {
            return activationKey;
        }
    }

    /// <summary>
    /// Returns the registered computer key
    /// </summary>
    public string ComputerKey
    {
        get
        {
            return computerKey;
        }            
    }

    public bool IsEvaluation
    {
        get
        {
            return isEvaluation;
        }
    }

    public bool EvaluationExpired
    {
        get
        {
            return evaluationExpired;
        }
    }

    public int EvaluationRemainingDays
    {
        get
        {
            return evaluationRemainingDays;
        }
    }

    /// <summary>
    /// Returns the underlying license object
    /// </summary>
    public QlmLicense QlmLicenseObject
    {
        get
        {
            return license;
        }
    }

    public bool WrongProductVersion
    {
        get
        {
            return wrongProductVersion;
        }

        set
        {
            wrongProductVersion = value;
        }
    }

    public string CustomData1
    {
        get
        {
            return customData1;
        }

        set
        {
            customData1 = value;
        }
    }

    public string CustomData2
    {
        get
        {
            return customData2;
        }

        set
        {
            customData2 = value;
        }
    }

    public string CustomData3
    {
        get
        {
            return customData3;
        }

        set
        {
            customData3 = value;
        }
    }

    public EServerErrorCode ServerErrorCode
    {
        get { return serverErrorCode; }
    }
    public ILicenseInfo ServerLicenseInfo
    {
        get { return serverLicenseInfo; }
    }
    public string ServerMessage
    {
        get { return serverMessage; }
    }
    #endregion

    #region Product Properties
    /// <summary>
    /// every time we activate a license, get the product properties from the server
    /// and write them to a local file
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public bool WriteProductPropertiesFile(out string errorMessage)
    {
        var ret = false;

        errorMessage = string.Empty;

        try
        {
            // store the license file - you may want to customize the destination folder
            var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var licenseFile = Path.Combine(docsFolder, productPropertiesFileName);

            // WriteProductPropertiesFile contacts the server, gets the product properties
            // and writes them to a digitally signed xml file
            license.WriteProductPropertiesFile(this.ActivationKey, licenseFile, out errorMessage);
                
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        return ret;

    }
    public IQlmProductProperties ReadProductPropertiesFile(out string errorMessage)
    {
        errorMessage = string.Empty;

        IQlmProductProperties pps = null;

        try
        {
            // store the license file - you may want to customize the destination folder
            var docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var licenseFile = Path.Combine(docsFolder, productPropertiesFileName);

            pps = license.ReadProductPropertiesFile(licenseFile, out errorMessage);

        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        return pps;
    }

    public void PublishAnalyticsToServer(string computerID, string customData1, string customData2, string customData3)
    {
        if (license.PublishAnalytics)
        {
            try
            {
                var analytics = new QlmAnalytics(license);

                var installID = analytics.ReadInstallID(out var errorMessage);

                var assemblyName = Assembly.GetEntryAssembly().GetName();
                var version = assemblyName.Version.ToString();

                if (string.IsNullOrEmpty(installID))
                {
                    var ret = analytics.AddInstallEx(version, analytics.GetOperatingSystem(),
                        Environment.MachineName, computerID,
                        activationKey, computerKey, license.IsEvaluation(),
                        license.ProductName, license.MajorVersion, license.MinorVersion,
                        customData1, customData2, customData3,
                        ref installID);

                    if (ret)
                    {
                        analytics.WriteInstallID(installID, out errorMessage);
                    }
                }
                else
                {
                    analytics.UpdateInstallEx(installID,
                        version, analytics.GetOperatingSystem(),
                        Environment.MachineName, computerID,
                        activationKey, this.computerKey, license.IsEvaluation(),
                        license.ProductName, license.MajorVersion, license.MinorVersion,
                        customData1, customData2, customData3
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    public void UnpublishAnalyticsFromServer()
    {
        if (license.PublishAnalytics)
        {
            try
            {
                var analytics = new QlmAnalytics(license);

                var installID = analytics.ReadInstallID(out var errorMessage);

                if (!string.IsNullOrEmpty(installID))
                {
                    if (analytics.RemoveInstall(installID, out errorMessage))
                    {
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Compares flags
    /// </summary>
    /// <param name="nVal1">Value 1</param>
    /// <param name="nVal2">Value 2</param>
    /// <returns></returns>
    private bool IsTrue(int nVal1, int nVal2)
    {
        return (nVal1 & nVal2) == nVal1 || (nVal1 & nVal2) == nVal2;
    }

    public static bool ServerCertificateValidator(object sender, X509Certificate certificate, X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    #endregion

}