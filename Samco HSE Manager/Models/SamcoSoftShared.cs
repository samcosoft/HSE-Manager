using DevExpress.Xpo.Metadata;
using DeviceId;
using LogicNP.CryptoLicensing;
using Newtonsoft.Json.Linq;
using OtpNet;
using Samco_HSE.HSEData;
using Samco_HSE_Manager.Locales;
using Syncfusion.Blazor.Grids;
using Syncfusion.Blazor.Popups;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Samco_HSE_Manager;

public static class SamcoSoftShared
{
    public static User? CurrentUser;
    public static int CurrentUserId;
    public static SiteRoles CurrentUserRole;
    public static CryptoLicense Lic = null!;

    public static ReflectionDictionary GetDatabaseAssemblies()
    {
        var dict = new ReflectionDictionary();
        dict.GetDataStoreSchema(Assembly.GetExecutingAssembly());
        return dict;
    }

    #region Licensing

    public static CryptoLicense CreateLicense(string storagePath)
    {
        return new CryptoLicense(LicenseStorageMode.ToIsolatedStorage, storagePath,
            "AMAAMACdh1RcSGEqKtdgSbVEqzvKCEObyWeh6GkCerfjPoqKjfctGktOUzbEMSjLNp70iLEDAAEAAQ==")
        {
            LicenseServiceURL = "https://demo.samcosoft.ir/activation2/service.asmx",
            LicenseServiceSettingsFilePath = "%AppDomainAppPath%App_Data\\Samco HSE.xml",
            IsolatedStoragePath = storagePath
        };
    }
    public static bool CheckLicense(out LicenseStatus status, bool isDevelopment = false)
    {
        Lic.Load();
        if (string.IsNullOrEmpty(Lic.LicenseCode))
        {
            //Set trial license
            Lic.LicenseCode = "FgIAADPatQM0YtkBHgB/RP5+IH5D5kgzoyBNbVnSETrzb2JLEJvWe2SNzNkOAK73ZNEEmp7eqeKAaTmqgRc=";
        }

        try
        {
            status = Lic.Status;
            if (isDevelopment && status == LicenseStatus.DebuggerDetected) return true;
            return status is LicenseStatus.Valid or LicenseStatus.GenericFailure;
        }
        catch (Exception)
        {
            status = LicenseStatus.InValid;
            return false;
        }
    }

    public static bool ActivateLicense(string serialNumber, out string returnMsg)
    {
        if (InternetCheck() == false)
        {
            returnMsg = Resource.ResourceManager.GetString("SamcosoftShared.NoInternet")!;
            //returnMsg = "ارتباط با اینترنت برقرار نیست.";
            return false;
        }

        var res = Lic.GetLicenseFromSerial(serialNumber);
        switch (res)
        {
            case SerialValidationResult.Success:
                returnMsg = Resource.ResourceManager.GetString("SamcoSoftShared_ActivateLicense_Activated")!;
                Lic.Save();
                return Lic.Status == LicenseStatus.Valid;
            case SerialValidationResult.Failed:
                returnMsg = Resource.ResourceManager.GetString("SamcoSoftShared_ActivateLicense_NoSuccess")!;
                return false;
            case SerialValidationResult.NotASerial:
                returnMsg = Resource.ResourceManager.GetString("SamcoSoftShared_ActivateLicense_SerialWrong")!;
                return false;
            default:
                returnMsg = Resource.ResourceManager.GetString("SamcoSoftShared_ActivateLicense_SerialWrong")!;
                return false;
        }
    }

    public static string? LicenseStatusMessage(LicenseStatus status)
    {
        switch (status)
        {
            case LicenseStatus.Valid:
                break;
            case LicenseStatus.NotValidated:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_NotChecked");
            case LicenseStatus.SerialCodeInvalid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_SerialInvalid");
            case LicenseStatus.SignatureInvalid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_SignatureInvalid");
            case LicenseStatus.MachineCodeInvalid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_MachineCodeInvalid");
            case LicenseStatus.Expired:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_Expired");
            case LicenseStatus.UsageModeInvalid:
                break;
            case LicenseStatus.ActivationFailed:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_ActivationFailed");
            case LicenseStatus.UsageDaysExceeded:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_UsageDaysExceeded");
            case LicenseStatus.UniqueUsageDaysExceeded:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_UniqueUsageDaysExceeded");
            case LicenseStatus.ExecutionsExceeded:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_ExecutionsExceeded");
            case LicenseStatus.EvaluationlTampered:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_EvaluationTampered");
            case LicenseStatus.GenericFailure:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_GenericFailure");
            case LicenseStatus.InstancesExceeded:
                break;
            case LicenseStatus.RunTimeExceeded:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_RunTimeExceeded");
            case LicenseStatus.CumulativeRunTimeExceeded:
                break;
            case LicenseStatus.ServiceNotificationFailed:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_ServiceNotificationFailed");
            case LicenseStatus.HostAssemblyDifferent:
                break;
            case LicenseStatus.StrongNameVerificationFailed:
                break;
            case LicenseStatus.Deactivated:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_Deactivated");
            case LicenseStatus.DebuggerDetected:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_DebuggerDetected");
            case LicenseStatus.DomainInvalid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_DomainInvalid");
            case LicenseStatus.DateRollbackDetected:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_DateRollbackDetected");
            case LicenseStatus.LocalTimeInvalid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_LocalTimeInvalid");
            case LicenseStatus.CryptoLicensingModuleTampered:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_CryptoLicensingModuleTampered");
            case LicenseStatus.RemoteSessionDetected:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_RemoteSessionDetected");
            case LicenseStatus.LicenseServerMachineCodeInvalid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_LicenseServerMachineCodeInvalid");
            case LicenseStatus.EvaluationDataLoadSaveFailed:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_EvaluationDataLoadSaveFailed");
            case LicenseStatus.InValid:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_InValid");
            case LicenseStatus.EvaluationExpired:
                return Resource.ResourceManager.GetString("SamcoSoftShared_LicenseStatusMessage_EvaluationExpired");
        }
        return string.Empty;
    }

    public static string GetDeviceId()
    {
        return new DeviceIdBuilder()
            .AddMachineName()
            .AddOsVersion()
            .OnWindows(windows => windows
                .AddProcessorId()
                .AddMotherboardSerialNumber())
            .ToString();
    }
    #endregion

    #region ParametersChecker

    public enum PasswordScore
    {
        Blank = 0,
        VeryWeak = 1,
        Weak = 2,
        Medium = 3,
        Strong = 4,
        VeryStrong = 5
    }

    public static PasswordScore PasswordStrengthChecker(string password)
    {
        var score = 1;

        switch (password.Length)
        {
            case < 1:
                return PasswordScore.Blank;
            case < 4:
                return PasswordScore.VeryWeak;
            case >= 8:
                score += 1;
                break;
        }

        if (password.Length >= 12) score += 1;
        if (Regex.IsMatch(password, "\\d+", RegexOptions.ECMAScript)) score += 1;
        if (Regex.IsMatch(password, "[a-z]", RegexOptions.ECMAScript) && Regex.IsMatch(password, "[A-Z]", RegexOptions.ECMAScript)) score += 1;
        if (Regex.IsMatch(password, "[!,@,#,$,%,^,&,*,?,_,~,-,£,(,)]", RegexOptions.ECMAScript)) score += 1;

        return (PasswordScore)score;
    }

    public static bool EmailAddressChecker(string emailAddress)
    {
        const string regExPattern = "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,4}$";
        return Regex.Match(emailAddress, regExPattern).Success;
    }

    public static bool ValidEmailChecker(string emailAddress, out string? message)
    {
        message = string.Empty;
        var netClient = new HttpClient();
        var result = netClient.GetStringAsync("https://www.validator.pizza/email/" + emailAddress);
        result.Wait();
        dynamic emailRes = JObject.Parse(result.Result);
        switch (emailRes.status)
        {
            case 200:
                if (emailRes.disposable)
                {
                    message = Resource.ResourceManager.GetString("SamcoSoftShared_ValidEmailChecker_DisposableEmail");
                    return false;
                }

                return true;
            case 400:
                message = "آدرس ایمیل معتبر نیست.";
                break;
            case 429:
                message = Resource.ResourceManager.GetString("SamcoSoftShared_ValidEmailChecker_RateLimitExceeded");
                break;
        }

        return false;
    }

    public static string ToEnglishNumber(string input)
    {
        var englishNumbers = "";

        for (var i = 0; i < input.Length; i++)
            if (char.IsDigit(input[i]))
                englishNumbers += char.GetNumericValue(input, i);
            else
                englishNumbers += input[i].ToString();

        return englishNumbers;
    }

    public static readonly Dictionary<string, string> Roles = new()
    {
        { "Admin", Resource.ResourceManager.GetString("AdminText")!},
        { "Officer", Resource.ResourceManager.GetString("OfficerText")! },
        { "Supervisor", Resource.ResourceManager.GetString("SupervisorText")! },
        { "Medic", Resource.ResourceManager.GetString("MedicText")! },
        { "Teacher", Resource.ResourceManager.GetString("TeacherText")! },
        { "Disabled", Resource.ResourceManager.GetString("DisabledText")! }
    };

    #endregion

    #region GeneralMethods
    public enum SiteRoles
    {
        Owner,
        Admin,
        Supervisor,
        Officer,
        Medic,
        Teacher,
        Personnel,
        Disabled
    }

    public static string GetRoleName(SiteRoles roles)
    {
        return roles switch
        {
            SiteRoles.Owner => Resource.ResourceManager.GetString("OwnerText")!,
            SiteRoles.Admin => Resource.ResourceManager.GetString("AdminText")!,
            SiteRoles.Supervisor => Resource.ResourceManager.GetString("SupervisorText")!,
            SiteRoles.Officer => Resource.ResourceManager.GetString("OfficerText")!,
            SiteRoles.Medic => Resource.ResourceManager.GetString("MedicText")!,
            SiteRoles.Teacher => Resource.ResourceManager.GetString("TeacherText")!,
            SiteRoles.Personnel => Resource.ResourceManager.GetString("PersonnelText")!,
            SiteRoles.Disabled => Resource.ResourceManager.GetString("DisabledText")!,
            _ => string.Empty
        };
    }

    public enum PersonnelStatus
    {
        Active,
        Inactive,
        Transferred
    }

    public static string GetPersonnelStatus(PersonnelStatus status)
    {
        return status switch
        {
            PersonnelStatus.Active => Resource.ResourceManager.GetString("SamcoSoftShared_GetPersonnelStatus_Active")!,
            PersonnelStatus.Inactive => Resource.ResourceManager.GetString("SamcoSoftShared_GetPersonnelStatus_Inactive")!,
            PersonnelStatus.Transferred => Resource.ResourceManager.GetString("SamcoSoftShared_GetPersonnelStatus_Transferred")!,
            _ => string.Empty
        };
    }

    public static readonly DialogSettings GeneralGridEditOption = new()
    {
        Width = "80%",
        AnimationEffect = DialogEffect.Zoom,
        ShowCloseIcon = true,
        AllowDragging = true,
        CloseOnEscape = true,
        Target = "#mainWindow",
        YValue = "top"
    };

    public enum UploadFolders
    {
        StopCards,
        Medical,
        Forms
    }
    public static T PickRandom<T>(this IEnumerable<T> source)
    {
        return source.PickRandom(1).Single();
    }

    private static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
    {
        return source.Shuffle().Take(count);
    }

    private static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        return source.OrderBy(_ => Guid.NewGuid());
    }

    public static bool InternetCheck()
    {
        try
        {
            var url = new Uri("http://demo.samcosoft.ir/");
            var client = new HttpClient { BaseAddress = url, Timeout = new TimeSpan(0, 0, 10) };
            using var response = client.GetAsync(url);
            response.Wait();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Encryption

    public enum PassCharacterSwitch
    {
        All,
        Number,
        UpperText,
        LowerText,
        TextAndNumber,
        UpperAndNumber
    }

    public static string? GenerateRandomPass(int length, PassCharacterSwitch @switch)
    {
        return GenerateRandomPass(length, length, @switch);
    }

    private static string? GenerateRandomPass(int minLength, int maxLength, PassCharacterSwitch @switch = PassCharacterSwitch.All)
    {
        // Define supported password characters divided into groups.
        // You can add (or remove) characters to (from) these groups.

        const string passwordCharsLcase = "abcdefgijkmnopqrstwxyz";
        const string passwordCharsUcase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string passwordCharsNumeric = "0123456789";
        const string passwordCharsSpecial = "*$-+?_&=!%{}/";

        // Make sure that input parameters are valid.
        if (minLength <= 0 || maxLength <= 0 || minLength > maxLength) return null;

        // Create a local array containing supported password characters
        // grouped by types. You can remove character groups from this
        // array, but doing so will weaken the password strength.
        var charGroups = @switch switch
        {
            PassCharacterSwitch.All => new[]
            {
                passwordCharsLcase.ToCharArray(),
                passwordCharsUcase.ToCharArray(),
                passwordCharsNumeric.ToCharArray(),
                passwordCharsSpecial.ToCharArray()
            },
            PassCharacterSwitch.Number => new[] { passwordCharsNumeric.ToCharArray() },
            PassCharacterSwitch.UpperText =>
                new[] { passwordCharsUcase.ToCharArray() },
            PassCharacterSwitch.LowerText =>
                new[] { passwordCharsLcase.ToCharArray() },
            PassCharacterSwitch.TextAndNumber => new[]
            {
                passwordCharsLcase.ToCharArray(),
                passwordCharsUcase.ToCharArray(),
                passwordCharsNumeric.ToCharArray()
            },
            PassCharacterSwitch.UpperAndNumber => new[]
            {
                passwordCharsUcase.ToCharArray(),
                passwordCharsNumeric.ToCharArray()
            },
            _ => throw new NotImplementedException()
        };
        //Use this array to track the number of unused characters in each
        // character group.
        var charsLeftInGroup = new int[charGroups.Length];

        // Initially, all characters in each group are not used.
        int I;
        for (I = 0; I < charsLeftInGroup.Length; I++) charsLeftInGroup[I] = charGroups[I].Length;

        // Use this array to track (iterate through) unused character groups.
        var leftGroupsOrder = new int[charGroups.Length];

        // Initially, all character groups are not used.
        for (I = 0; I < leftGroupsOrder.Length; I++) leftGroupsOrder[I] = I;

        // Because we cannot use the default randomization, which is based on the
        // current time (it will produce the same "random" number within a
        // second), we will use a random number generator to seed the
        // randomization.

        // Use a 4-byte array to fill it with random bytes and convert it then
        // to an integer value.
        //var randomBytes = RandomNumberGenerator.GetBytes(4);
        var randomBytes = RandomNumberGenerator.GetBytes(4);

        // Convert 4 bytes into a 32-bit integer value.
        var seed = BitConverter.ToInt32(randomBytes, 0);

        // Now, this is real randomization.
        var random = new Random(seed);

        // This array will hold password characters.

        // Allocate appropriate memory for the password.
        var password = minLength < maxLength ? new char[random.Next(minLength - 1, maxLength) + 1] : new char[minLength];

        // Index of the next character to be added to password.

        // Index of the next character group to be processed.

        // Index which will be used to track not processed character groups.

        // Index of the last non-processed character in a group.

        // Index of the last non-processed group.
        var lastLeftGroupsOrderIdx = leftGroupsOrder.Length - 1;

        // Generate password characters one at a time.
        for (I = 0; I < password.Length; I++)
        {
            // If only one character group remained unprocessed, process it;
            // otherwise, pick a random character group from the unprocessed
            // group list. To allow a special character to appear in the
            // first position, increment the second parameter of the Next
            // function call by one, i.e. lastLeftGroupsOrderIdx + 1.
            var nextLeftGroupsOrderIdx = lastLeftGroupsOrderIdx == 0 ? 0 : random.Next(0, lastLeftGroupsOrderIdx);

            // Get the actual index of the character group, from which we will
            // pick the next character.
            var nextGroupIdx = leftGroupsOrder[nextLeftGroupsOrderIdx];

            // Get the index of the last unprocessed characters in this group.
            var lastCharIdx = charsLeftInGroup[nextGroupIdx] - 1;

            // If only one unprocessed character is left, pick it; otherwise,
            // get a random character from the unused character list.
            var nextCharIdx = lastCharIdx == 0 ? 0 : random.Next(0, lastCharIdx + 1);

            // Add this character to the password.
            password[I] = charGroups[nextGroupIdx][nextCharIdx];

            // If we processed the last character in this group, start over.
            if (lastCharIdx == 0)
            {
                charsLeftInGroup[nextGroupIdx] = charGroups[nextGroupIdx].Length;
                // There are more unprocessed characters left.
            }
            else
            {
                // Swap processed character with the last unprocessed character
                // so that we don't pick it until we process all characters in
                // this group.
                if (lastCharIdx != nextCharIdx)
                {
                    (charGroups[nextGroupIdx][lastCharIdx], charGroups[nextGroupIdx][nextCharIdx]) = (charGroups[nextGroupIdx][nextCharIdx], charGroups[nextGroupIdx][lastCharIdx]);
                }

                // Decrement the number of unprocessed characters in
                // this group.
                charsLeftInGroup[nextGroupIdx] = charsLeftInGroup[nextGroupIdx] - 1;
            }

            // If we processed the last group, start all over.
            if (lastLeftGroupsOrderIdx == 0)
            {
                lastLeftGroupsOrderIdx = leftGroupsOrder.Length - 1;
                // There are more unprocessed groups left.
            }
            else
            {
                // Swap processed group with the last unprocessed group
                // so that we don't pick it until we process all groups.
                if (lastLeftGroupsOrderIdx != nextLeftGroupsOrderIdx)
                {
                    var temp = leftGroupsOrder[lastLeftGroupsOrderIdx];
                    leftGroupsOrder[lastLeftGroupsOrderIdx] = leftGroupsOrder[nextLeftGroupsOrderIdx];
                    leftGroupsOrder[nextLeftGroupsOrderIdx] = temp;
                }

                // Decrement the number of unprocessed groups.
                lastLeftGroupsOrderIdx -= 1;
            }
        }

        // Convert password characters into a string and return the result.
        return new string(password);
    }

    #endregion

    #region OTP

    public static string GetOtp(string secretKey, int timeOut = 120)
    {
        var secretKeyByte = Convert.FromBase64String(secretKey);
        //Generate OTP
        var totpGen = new Totp(secretKeyByte, timeOut);
        return totpGen.ComputeTotp();
    }

    public static bool VerifyOtp(string secretKey, string otpCode, int timeout = 120)
    {
        var secretKeyByte = Convert.FromBase64String(secretKey);
        var totpGen = new Totp(secretKeyByte, timeout);
        return totpGen.VerifyTotp(otpCode, out _, new VerificationWindow(previous: 1, future: 1));
    }

    #endregion
}