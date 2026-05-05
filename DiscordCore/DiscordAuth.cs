using DiscordCore.Native;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscordCore
{
    internal static unsafe class DiscordAuth
    {
        // Minimum scopes for Connect() + invite callbacks.
        // Verify current scope names at discord.com/developers/docs/topics/oauth2 if issues arise.
        internal const string OAuthScopes = "openid sdk.social_layer_presence";

        // Pinned delegate fields — MUST be fields to prevent GC collection while native holds them.
        private static Discord_Client_AuthorizationCallback _authorizeCallback;
        private static Discord_Client_TokenExchangeCallback _getTokenCallback;
        private static Discord_Client_TokenExchangeCallback _refreshCallback;
        private static Discord_Client_UpdateTokenCallback _updateTokenCallback;
        private static Discord_Client_RevokeTokenCallback _revokeCallback;
        private static Discord_Client_TokenExpirationCallback _tokenExpiryCallback;

        // Kept alive across the authorize → getToken async flow.
        private static Discord_AuthorizationCodeVerifier _verifier;
        private static bool _verifierAlive;

        private static Action<bool, string> _pendingAuthorizeCallback;
        private static Action _pendingRevokeCallback;
        private static Action _pendingConnectCallback;

        // Called from DiscordClient.Enable() after InitClient() — loads stored token if valid.
        internal static void TryConnect()
        {
            if (!DiscordClient._clientInitialized) return;
            if (SDK.Discord_Client_IsAuthenticated(ref DiscordClient._client)) return;

            var tokens = Config.Instance.GetTokens(DiscordClient.CurrentAppID);
            if (tokens != null && tokens.HasValidToken)
                DoUpdateTokenAndConnect(tokens.AccessToken, null);
            else if (tokens != null && tokens.HasRefreshToken)
                Refresh();
        }

        // Registers the token-expiry callback on the client. Call once from InitClient().
        internal static void RegisterExpiryCallback()
        {
            _tokenExpiryCallback = OnTokenExpired;
            SDK.Discord_Client_SetTokenExpirationCallback(
                ref DiscordClient._client, _tokenExpiryCallback, DiscordClient._nullFreeFn, IntPtr.Zero);
        }

        // Starts the PKCE authorization flow. Opens a Discord overlay/browser for user consent.
        internal static void Authorize(Action<bool, string> callback)
        {
            if (DiscordClient.CurrentAppID <= 0)
            {
                callback?.Invoke(false, "No active app ID — ensure at least one mod is registered with DiscordCore");
                return;
            }
            if (!DiscordClient._clientInitialized)
            {
                callback?.Invoke(false, "Discord client not initialized");
                return;
            }

            _pendingAuthorizeCallback = callback;

            SDK.Discord_Client_CreateAuthorizationCodeVerifier(ref DiscordClient._client, out _verifier);
            _verifierAlive = true;

            SDK.Discord_AuthorizationCodeVerifier_Challenge(ref _verifier, out var challenge);

            Discord_AuthorizationArgs args;
            SDK.Discord_AuthorizationArgs_Init(out args);
            try
            {
                SDK.Discord_AuthorizationArgs_SetClientId(ref args, (ulong)DiscordClient.CurrentAppID);

                byte[] scopeBytes = Encoding.UTF8.GetBytes(OAuthScopes);
                fixed (byte* p = scopeBytes)
                    SDK.Discord_AuthorizationArgs_SetScopes(ref args, Discord_String.From(p, scopeBytes.Length));

                SDK.Discord_AuthorizationArgs_SetCodeChallenge(ref args, ref challenge);

                _authorizeCallback = OnNativeAuthorize;
                SDK.Discord_Client_Authorize(
                    ref DiscordClient._client, ref args,
                    _authorizeCallback, DiscordClient._nullFreeFn, IntPtr.Zero);
            }
            finally
            {
                SDK.Discord_AuthorizationArgs_Drop(ref args);
                SDK.Discord_AuthorizationCodeChallenge_Drop(ref challenge);
            }
        }

        private static void OnNativeAuthorize(IntPtr resultPtr, Discord_String code, Discord_String redirectUri, IntPtr userData)
        {
            var result = Marshal.PtrToStructure<Discord_ClientResult>(resultPtr);
            if (!SDK.Discord_ClientResult_Successful(ref result))
            {
                string err = GetResultError(ref result);
                Plugin.log.Error($"[DISCORD] Authorize failed: {err}");
                _pendingAuthorizeCallback?.Invoke(false, err);
                _pendingAuthorizeCallback = null;
                DropVerifier();
                return;
            }

            // Copy strings to managed before any native memory could be freed.
            string codeStr = MarshalString(code);
            string redirectStr = MarshalString(redirectUri);

            // Read verifier string from the PKCE verifier object — must happen before Drop.
            Discord_String verifierOut;
            SDK.Discord_AuthorizationCodeVerifier_Verifier(ref _verifier, &verifierOut);
            string verifierStr = MarshalString(verifierOut);
            DropVerifier();

            byte[] codeBytes = Encoding.UTF8.GetBytes(codeStr);
            byte[] verifierBytes = Encoding.UTF8.GetBytes(verifierStr);
            byte[] redirectBytes = Encoding.UTF8.GetBytes(redirectStr);

            fixed (byte* pCode = codeBytes, pVerifier = verifierBytes, pRedirect = redirectBytes)
            {
                _getTokenCallback = OnNativeGetToken;
                SDK.Discord_Client_GetToken(
                    ref DiscordClient._client,
                    (ulong)DiscordClient.CurrentAppID,
                    Discord_String.From(pCode, codeBytes.Length),
                    Discord_String.From(pVerifier, verifierBytes.Length),
                    Discord_String.From(pRedirect, redirectBytes.Length),
                    _getTokenCallback, DiscordClient._nullFreeFn, IntPtr.Zero);
            }
        }

        private static void OnNativeGetToken(
            IntPtr resultPtr,
            Discord_String accessToken, Discord_String refreshToken,
            Discord_AuthorizationTokenType tokenType, int expiresIn,
            Discord_String scopes, IntPtr userData)
        {
            var result = Marshal.PtrToStructure<Discord_ClientResult>(resultPtr);
            if (!SDK.Discord_ClientResult_Successful(ref result))
            {
                string err = GetResultError(ref result);
                Plugin.log.Error($"[DISCORD] GetToken failed: {err}");
                _pendingAuthorizeCallback?.Invoke(false, err);
                _pendingAuthorizeCallback = null;
                return;
            }

            string access = MarshalString(accessToken);
            string refresh = MarshalString(refreshToken);
            StoreTokens(access, refresh, expiresIn);

            var cb = _pendingAuthorizeCallback;
            _pendingAuthorizeCallback = null;
            DoUpdateTokenAndConnect(access, () => cb?.Invoke(true, null));
        }

        internal static void Refresh()
        {
            if (!DiscordClient._clientInitialized) return;
            var tokens = Config.Instance.GetTokens(DiscordClient.CurrentAppID);
            string refreshToken = tokens != null ? tokens.RefreshToken : null;
            if (string.IsNullOrEmpty(refreshToken)) return;

            byte[] bytes = Encoding.UTF8.GetBytes(refreshToken);
            fixed (byte* p = bytes)
            {
                _refreshCallback = OnNativeRefresh;
                SDK.Discord_Client_RefreshToken(
                    ref DiscordClient._client,
                    (ulong)DiscordClient.CurrentAppID,
                    Discord_String.From(p, bytes.Length),
                    _refreshCallback, DiscordClient._nullFreeFn, IntPtr.Zero);
            }
        }

        private static void OnNativeRefresh(
            IntPtr resultPtr,
            Discord_String accessToken, Discord_String refreshToken,
            Discord_AuthorizationTokenType tokenType, int expiresIn,
            Discord_String scopes, IntPtr userData)
        {
            var result = Marshal.PtrToStructure<Discord_ClientResult>(resultPtr);
            if (!SDK.Discord_ClientResult_Successful(ref result))
            {
                Plugin.log.Warn($"[DISCORD] Token refresh failed: {GetResultError(ref result)}. Please reconnect in Settings.");
                ClearTokens();
                return;
            }

            string access = MarshalString(accessToken);
            string refresh = MarshalString(refreshToken);
            StoreTokens(access, refresh, expiresIn);
            DoUpdateTokenAndConnect(access, null);
        }

        internal static void Revoke(Action callback)
        {
            _pendingRevokeCallback = callback;

            var tokens = Config.Instance.GetTokens(DiscordClient.CurrentAppID);
            string accessToken = tokens != null ? tokens.AccessToken : null;
            if (!DiscordClient._clientInitialized || string.IsNullOrEmpty(accessToken))
            {
                ClearTokens();
                _pendingRevokeCallback?.Invoke();
                _pendingRevokeCallback = null;
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(accessToken);
            fixed (byte* p = bytes)
            {
                _revokeCallback = OnNativeRevoke;
                SDK.Discord_Client_RevokeToken(
                    ref DiscordClient._client,
                    (ulong)DiscordClient.CurrentAppID,
                    Discord_String.From(p, bytes.Length),
                    _revokeCallback, DiscordClient._nullFreeFn, IntPtr.Zero);
            }
        }

        private static void OnNativeRevoke(IntPtr resultPtr, IntPtr userData)
        {
            var result = Marshal.PtrToStructure<Discord_ClientResult>(resultPtr);
            if (!SDK.Discord_ClientResult_Successful(ref result))
                Plugin.log.Warn($"[DISCORD] RevokeToken failed: {GetResultError(ref result)}");

            ClearTokens();
            DiscordClient.SetAuthenticated(false);
            if (DiscordClient._clientInitialized)
                SDK.Discord_Client_Disconnect(ref DiscordClient._client);

            _pendingRevokeCallback?.Invoke();
            _pendingRevokeCallback = null;
        }

        private static void OnTokenExpired(IntPtr userData)
        {
            var tokens = Config.Instance.GetTokens(DiscordClient.CurrentAppID);
            if (tokens != null && tokens.HasRefreshToken)
            {
                Plugin.log.Info("[DISCORD] Access token expired, refreshing automatically...");
                Refresh();
            }
            else
            {
                Plugin.log.Warn("[DISCORD] Access token expired and no refresh token stored. Please reconnect in Settings.");
                ClearTokens();
            }
        }

        private static void DoUpdateTokenAndConnect(string token, Action onConnected)
        {
            _pendingConnectCallback = onConnected;
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            fixed (byte* p = bytes)
            {
                _updateTokenCallback = OnNativeUpdateToken;
                SDK.Discord_Client_UpdateToken(
                    ref DiscordClient._client,
                    Discord_AuthorizationTokenType.Bearer,
                    Discord_String.From(p, bytes.Length),
                    _updateTokenCallback, DiscordClient._nullFreeFn, IntPtr.Zero);
            }
        }

        private static void OnNativeUpdateToken(IntPtr resultPtr, IntPtr userData)
        {
            var result = Marshal.PtrToStructure<Discord_ClientResult>(resultPtr);
            if (SDK.Discord_ClientResult_Successful(ref result))
            {
                Plugin.log.Info("[DISCORD] Token accepted, connecting...");
                DiscordClient.SetAuthenticated(true);
                SDK.Discord_Client_Connect(ref DiscordClient._client);
                _pendingConnectCallback?.Invoke();
                _pendingConnectCallback = null;
            }
            else
            {
                Plugin.log.Error($"[DISCORD] UpdateToken failed: {GetResultError(ref result)}");
                ClearTokens();
                _pendingConnectCallback = null;
            }
        }

        private static void StoreTokens(string access, string refresh, int expiresIn)
        {
            long expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;
            Config.Instance.SaveAuthTokens(DiscordClient.CurrentAppID, access, refresh, expiry);
        }

        private static void ClearTokens()
        {
            DiscordClient.SetAuthenticated(false);
            Config.Instance.ClearAuthTokens(DiscordClient.CurrentAppID);
        }

        private static void DropVerifier()
        {
            if (_verifierAlive)
            {
                SDK.Discord_AuthorizationCodeVerifier_Drop(ref _verifier);
                _verifierAlive = false;
            }
        }

        internal static string MarshalString(Discord_String ds)
        {
            if (ds.ptr == null || (int)ds.size == 0) return string.Empty;
            return Encoding.UTF8.GetString(ds.ptr, (int)ds.size);
        }

        private static string GetResultError(ref Discord_ClientResult result)
        {
            Discord_String errStr;
            SDK.Discord_ClientResult_Error(ref result, &errStr);
            return errStr.ptr != null ? Encoding.UTF8.GetString(errStr.ptr, (int)errStr.size) : "Unknown error";
        }
    }
}
