using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DevExpress.Blazor;
using DevExpress.Export;
using DevExpress.Export.Xl;
using DevExpress.Printing.ExportHelpers;
using DevExpress.Xpo.Metadata;
using Newtonsoft.Json.Linq;
using OtpNet;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager
{
    public static class SamcoSoftShared
    {
        public static User? CurrentUser;
        public static int CurrentUserId;
        public static SiteRoles CurrentUserRole;
        public static ReflectionDictionary GetDatabaseAssemblies()
        {
            var dict = new ReflectionDictionary();
            dict.GetDataStoreSchema(System.Reflection.Assembly.GetExecutingAssembly());
            return dict;
        }

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

        public enum OrderModes
        {
            User,
            Photo,
            Online
        }

        public enum OrderStatus
        {
            Registered,
            Approved,
            Paid,
            WaitForSampler,
            InProgressed,
            Completed,
            Cancelled
        }

        public static string GetStatusText(string EnglishStatus)
        {
            var Stat = Enum.Parse<OrderStatus>(EnglishStatus);
            return Stat switch
            {
                OrderStatus.Registered => "در انتظار پذیرش...",
                OrderStatus.Approved => "در انتظار پرداخت...",
                OrderStatus.Paid => "در انتظار نمونه گیری...",
                OrderStatus.WaitForSampler => "در حال نمونه گیری...",
                OrderStatus.InProgressed => "در حال انجام...",
                OrderStatus.Completed => "کامل شده",
                OrderStatus.Cancelled => "لغو شده",
                _ => null
            };
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
            const string RegExPattern = "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,4}$";
            return Regex.Match(emailAddress, RegExPattern).Success;
        }

        public static bool ValidEmailChecker(string EmailAddress, out string Message)
        {
            Message = string.Empty;
            var NetClient = new HttpClient();
            var Result = NetClient.GetStringAsync("https://www.validator.pizza/email/" + EmailAddress);
            Result.Wait();
            dynamic EmailRes = JObject.Parse(Result.Result);
            switch (EmailRes.status)
            {
                case 200:
                    if (EmailRes.disposable)
                    {
                        Message = "ایمیل وارد شده از نوع یکبار مصرف است و استفاده از آن خلاف قوانین سیستم است.";
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                case 400:
                    Message = "آدرس ایمیل معتبر نیست.";
                    return false;
                case 429:
                    Message = "ظرفیت ثبت نام به پایان رسیده است.";
                    return false;
            }

            return false;
        }

        public static string ToEnglishNumber(string input)
        {
            var EnglishNumbers = "";

            for (var i = 0; i < input.Length; i++)
                if (char.IsDigit(input[i]))
                    EnglishNumbers += char.GetNumericValue(input, i);
                else
                    EnglishNumbers += input[i].ToString();

            return EnglishNumbers;
        }

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
            Disabled
        }

        public static T PickRandom<T>(this IEnumerable<T> source)
        {
            return source.PickRandom(1).Single();
        }

        public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
        {
            return source.Shuffle().Take(count);
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.OrderBy(_ => Guid.NewGuid());
        }

        #endregion

        #region GridCustomization
        public static void CustomizeSheet(GridExportCustomizeSheetEventArgs e)
        {
            e.Sheet.ViewOptions.RightToLeft = true;
        }
        public static void CustomizeCell(GridExportCustomizeCellEventArgs e)
        {
            e.Formatting.Font = new XlCellFont
            {
                Name = "Vazir",
                Size = 14
            };
            e.Handled = true;
        }
        public static void CustomizeFooter(GridExportCustomizeSheetHeaderFooterEventArgs e)
        {
            e.ExportContext.AddRow();

            // Create a new row.
            var firstRow = new CellObject
            {
                Value = "Powered by Samco HSE Manager"
            };
            var rowFormat = new XlFormattingObject
            {
                Font = new XlCellFont
                {
                    Name = "Vazir",
                    Size = 12,
                    Bold = true
                }
            };
            firstRow.Formatting = rowFormat;
            e.ExportContext.AddRow(new[] { firstRow });
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

        public static string GenerateRandomPass(int length, PassCharacterSwitch Switch)
        {
            return GenerateRandomPass(length, length, Switch);
        }

        private static string GenerateRandomPass(int minLength, int maxLength, PassCharacterSwitch Switch = PassCharacterSwitch.All)
        {
            // Define supported password characters divided into groups.
            // You can add (or remove) characters to (from) these groups.

            const string PASSWORD_CHARS_LCASE = "abcdefgijkmnopqrstwxyz";
            const string PASSWORD_CHARS_UCASE = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string PASSWORD_CHARS_NUMERIC = "0123456789";
            const string PASSWORD_CHARS_SPECIAL = "*$-+?_&=!%{}/";

            // Make sure that input parameters are valid.
            if (minLength <= 0 || maxLength <= 0 || minLength > maxLength) return null;

            // Create a local array containing supported password characters
            // grouped by types. You can remove character groups from this
            // array, but doing so will weaken the password strength.
            var charGroups = Switch switch
            {
                PassCharacterSwitch.All => new[]
                {
                    PASSWORD_CHARS_LCASE.ToCharArray(),
                    PASSWORD_CHARS_UCASE.ToCharArray(),
                    PASSWORD_CHARS_NUMERIC.ToCharArray(),
                    PASSWORD_CHARS_SPECIAL.ToCharArray()
                },
                PassCharacterSwitch.Number => new[] { PASSWORD_CHARS_NUMERIC.ToCharArray() },
                PassCharacterSwitch.UpperText =>
                    new[] { PASSWORD_CHARS_UCASE.ToCharArray() },
                PassCharacterSwitch.LowerText =>
                    new[] { PASSWORD_CHARS_LCASE.ToCharArray() },
                PassCharacterSwitch.TextAndNumber => new[]
                {
                    PASSWORD_CHARS_LCASE.ToCharArray(),
                    PASSWORD_CHARS_UCASE.ToCharArray(),
                    PASSWORD_CHARS_NUMERIC.ToCharArray()
                },
                PassCharacterSwitch.UpperAndNumber => new[]
                {
                    PASSWORD_CHARS_UCASE.ToCharArray(),
                    PASSWORD_CHARS_NUMERIC.ToCharArray()
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

            // Because we cannot use the default randomizer, which is based on the
            // current time (it will produce the same "random" number within a
            // second), we will use a random number generator to seed the
            // randomizer.

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
                        var temp = charGroups[nextGroupIdx][lastCharIdx];
                        charGroups[nextGroupIdx][lastCharIdx] = charGroups[nextGroupIdx][nextCharIdx];
                        charGroups[nextGroupIdx][nextCharIdx] = temp;
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

        public static string GetOTP(string SecretKey, int TimeOut = 120)
        {
            var SecretKeyByte = Convert.FromBase64String(SecretKey);
            //Generate OTP
            var TotpGen = new Totp(SecretKeyByte, TimeOut);
            return TotpGen.ComputeTotp();
        }

        public static bool VerifyOTP(string SecretKey, string OTPCode, int Timeout = 120)
        {
            var SecretKeyByte = Convert.FromBase64String(SecretKey);
            var TotpGen = new Totp(SecretKeyByte, Timeout);
            return TotpGen.VerifyTotp(OTPCode, out _, new VerificationWindow(previous: 1, future: 1));
        }

        #endregion
    }
}
