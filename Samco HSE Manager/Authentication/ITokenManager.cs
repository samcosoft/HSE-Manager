using System.Security.Claims;
using DevExpress.Xpo;
using Samco_HSE.HSEData;

namespace Samco_HSE_Manager.Authentication
{
    public interface ITokenManager
    {
        bool Authenticate(string? Username, string? Password, IDataLayer dataLayer, out string TokenString, out string RefreshToken, out string ErrorMessage);
        bool Authenticate(Personnel SelUser, IDataLayer dataLayer, out string TokenString, out string RefreshToken, out string ErrorMessage);
        ClaimsPrincipal VerifyToken(string token);
    }
}
