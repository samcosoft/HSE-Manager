using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using Microsoft.IdentityModel.Tokens;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Authentication
{
    public class TokenManager : ITokenManager
    {
        private readonly JwtSecurityTokenHandler _tokenHandler;
        private readonly byte[] _secretkey;
        private readonly string _issuer;
        private readonly string _audience;


        public TokenManager(string secretKey, string issuer, string audience)
        {
            _tokenHandler = new JwtSecurityTokenHandler();
            _secretkey = Encoding.ASCII.GetBytes(secretKey);
            _issuer = issuer;
            _audience = audience;
        }

        public bool Authenticate(string? username, string? password, IDataLayer dataLayer, out string tokenString, out string refreshToken, out string errorMessage)
        {
            tokenString = string.Empty;
            refreshToken = string.Empty;
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return false;

            using var session1 = new Session(dataLayer);

            var selUser = session1.FindObject<User>(new BinaryOperator(nameof(User.Username), username));

            try
            {
                if (selUser == null || BCrypt.Net.BCrypt.EnhancedVerify(password, selUser.Password) == false)
                {
                    errorMessage = "نام کاربری و یا کلمه عبور اشتباه است.";
                    return false;
                }

                if (selUser.SiteRole == SamcoSoftShared.SiteRoles.Disabled.ToString())
                {
                    errorMessage = "این کاربر توسط مدیر سیستم غیرفعال شده است.";
                    return false;
                }
            }
            catch (Exception)
            {
                errorMessage = "بروز خطا در سیستم. لطفاً دوباره تلاش کنید";
                return false;
            }

            //Generate Token

            var securityDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, selUser.Username ?? string.Empty),
                    new Claim(ClaimTypes.GivenName, selUser.PersonnelName ?? string.Empty),
                    new Claim(ClaimTypes.MobilePhone, selUser.MobileNum ?? string.Empty),
                    new Claim(ClaimTypes.Role,selUser.SiteRole?? string.Empty),
                    new Claim(ClaimTypes.Sid, selUser.Oid.ToString())
                }),
                Issuer = _issuer,
                Audience = _audience,
                Expires = DateTime.UtcNow.AddHours(6),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_secretkey), SecurityAlgorithms.HmacSha256Signature)
            };

            tokenString = _tokenHandler.WriteToken(_tokenHandler.CreateToken(securityDescriptor));
            refreshToken = GenerateRefreshToken(selUser);
            selUser.LastLogin = DateTime.Now;
            selUser.Save();
            return true;
        }

        public bool Authenticate(Personnel? selUser, IDataLayer dataLayer, out string tokenString, out string refreshToken, out string errorMessage)
        {
            tokenString = string.Empty;
            refreshToken = string.Empty;
            errorMessage = string.Empty;
            if (selUser == null) return false;

            //Generate Token

            var securityDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, selUser.NationalID ?? string.Empty),
                    new Claim(ClaimTypes.GivenName, selUser.PersonnelName ?? string.Empty),
                    new Claim(ClaimTypes.MobilePhone, selUser.MobileNum ?? string.Empty),
                    new Claim(ClaimTypes.Role,"Personnel"),
                    new Claim(ClaimTypes.Sid, selUser.Oid.ToString())
                }),
                Issuer = _issuer,
                Audience = _audience,
                Expires = DateTime.UtcNow.AddHours(6),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_secretkey), SecurityAlgorithms.HmacSha256Signature)
            };

            tokenString = _tokenHandler.WriteToken(_tokenHandler.CreateToken(securityDescriptor));
            //refreshToken = GenerateRefreshToken(selUser);
            selUser.Save();
            return true;
        }
        public ClaimsPrincipal VerifyToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return new ClaimsPrincipal();

            var claim = _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_secretkey),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ValidateAudience = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return claim;
        }
        private string GenerateRefreshToken(User selUser)
        {
            // generate token that is valid for 7 days
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            //var refreshToken = new RefreshTokens(SelUser.Session)
            //{
            //    Token = Convert.ToBase64String(randomBytes),
            //    Expires = DateTime.UtcNow.AddDays(7),
            //    User = SelUser
            //};

            //refreshToken.Save();

            return Convert.ToBase64String(randomBytes);
        }
    }
}
