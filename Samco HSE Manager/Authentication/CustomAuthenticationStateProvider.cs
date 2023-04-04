using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace Samco_HSE_Manager.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    public ILocalStorageService LocalStorageService { get; }
    public ITokenManager TokenManager { get; }

    public CustomAuthenticationStateProvider(ILocalStorageService localStorageService, ITokenManager tokenManager)
    {
        //throw new Exception("CustomAuthenticationStateProviderException");
        LocalStorageService = localStorageService;
        TokenManager = tokenManager;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var accessToken = await LocalStorageService.GetItemAsync<string>("accessToken");

            var identity = !string.IsNullOrEmpty(accessToken) ? TokenManager.VerifyToken(accessToken) : new ClaimsPrincipal();

            return await Task.FromResult(new AuthenticationState(identity));
        }
        catch (Exception)
        {
            var anonymous = new ClaimsIdentity();
            return await Task.FromResult(new AuthenticationState(new ClaimsPrincipal(anonymous)));
        }
    }

    public async Task MarkUserAsAuthenticated(string accessToken)
    {
        await LocalStorageService.SetItemAsync("accessToken", accessToken);

        var claimsPrincipal = new ClaimsPrincipal(TokenManager.VerifyToken(accessToken));

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
    }

    public async Task MarkUserAsLoggedOut()
    {
        await LocalStorageService.RemoveItemAsync("accessToken");

        var identity = new ClaimsIdentity();

        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }
}