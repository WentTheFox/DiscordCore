using System;
using System.Runtime.InteropServices;

namespace DiscordCore.Native
{
    // === Primitive native structs ===

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct Discord_String
    {
        internal byte* ptr;
        internal UIntPtr size; // size_t — correct for both 32-bit and 64-bit

        internal static Discord_String From(byte* ptr, int length)
        {
            return new Discord_String { ptr = ptr, size = (UIntPtr)length };
        }
    }

    // === Opaque handle structs ===
    // All major SDK types are { void* opaque } in C — represented as single-IntPtr structs.

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_Client { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_Activity { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_ActivityAssets { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_ActivityTimestamps { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_ActivityParty { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_ActivitySecrets { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_ActivityInvite { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_ClientResult { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_AuthorizationArgs { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_AuthorizationCodeChallenge { internal IntPtr opaque; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Discord_AuthorizationCodeVerifier { internal IntPtr opaque; }

    // === Native enums (values match cdiscord.h) ===

    internal enum Discord_Client_Status : int
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Ready = 3,
        Reconnecting = 4,
        Disconnecting = 5,
    }

    internal enum Discord_Client_Error : int
    {
        None = 0,
        ConnectionFailed = 1,
        UnexpectedClose = 2,
        ConnectionCanceled = 3,
    }

    internal enum Discord_LoggingSeverity : int
    {
        Verbose = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        None = 5,
    }

    internal enum Discord_ActivityActionTypes : int
    {
        Invalid = 0,
        Join = 1,
        JoinRequest = 5,
    }

    internal enum Discord_ActivityTypes : int
    {
        Playing = 0,
        Streaming = 1,
        Listening = 2,
        Watching = 3,
        CustomStatus = 4,
        Competing = 5,
    }

    internal enum Discord_ActivityPartyPrivacy : int
    {
        Private = 0,
        Public = 1,
    }

    internal enum Discord_ErrorType : int
    {
        None = 0,
        NetworkError = 1,
        HTTPError = 2,
        ClientNotReady = 3,
        Disabled = 4,
        ClientDestroyed = 5,
        ValidationError = 6,
        Aborted = 7,
        AuthorizationFailed = 8,
        RPCError = 9,
    }

    internal enum Discord_AuthorizationTokenType : int
    {
        User = 0,
        Bearer = 1,
    }

    // === Unmanaged callback delegate types ===
    // IMPORTANT: All instances used as native callbacks MUST be stored in fields.
    // If they are garbage-collected while native code holds the function pointer, the process crashes.

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_Client_OnStatusChanged(
        Discord_Client_Status status,
        Discord_Client_Error error,
        int errorDetail,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void Discord_Client_LogCallback(
        Discord_String message,
        Discord_LoggingSeverity severity,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_Client_UpdateRichPresenceCallback(
        IntPtr result,   // Discord_ClientResult* — use Marshal.PtrToStructure to read
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void Discord_Client_ActivityJoinCallback(
        Discord_String joinSecret,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_Client_ActivityInviteCallback(
        IntPtr invite,   // Discord_ActivityInvite* — use Marshal.PtrToStructure to read
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_FreeFn(IntPtr ptr);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void Discord_Client_AuthorizationCallback(
        IntPtr result,
        Discord_String code,
        Discord_String redirectUri,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void Discord_Client_TokenExchangeCallback(
        IntPtr result,
        Discord_String accessToken,
        Discord_String refreshToken,
        Discord_AuthorizationTokenType tokenType,
        int expiresIn,
        Discord_String scopes,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_Client_UpdateTokenCallback(IntPtr result, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_Client_RevokeTokenCallback(IntPtr result, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Discord_Client_TokenExpirationCallback(IntPtr userData);

    // === P/Invoke declarations ===

    internal static unsafe class SDK
    {
        private const string DllName = "discord_partner_sdk";

        // --- Global ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_RunCallbacks();

        // --- Discord_Client lifecycle ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_Init(ref Discord_Client self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_Drop(ref Discord_Client self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_Connect(ref Discord_Client self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_Disconnect(ref Discord_Client self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Discord_Client_Status Discord_Client_GetStatus(ref Discord_Client self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_SetApplicationId(ref Discord_Client self, ulong applicationId);

        // --- Discord_Client configuration ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_AddLogCallback(
            ref Discord_Client self,
            Discord_Client_LogCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData,
            Discord_LoggingSeverity minSeverity);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_SetStatusChangedCallback(
            ref Discord_Client self,
            Discord_Client_OnStatusChanged cb,
            Discord_FreeFn cbUserDataFree,
            IntPtr cbUserData);

        // --- Steam registration ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool Discord_Client_RegisterLaunchSteamApplication(
            ref Discord_Client self,
            ulong applicationId,
            uint steamAppId);

        // --- Rich presence ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_UpdateRichPresence(
            ref Discord_Client self,
            ref Discord_Activity activity,
            Discord_Client_UpdateRichPresenceCallback cb,
            Discord_FreeFn cbUserDataFree,
            IntPtr cbUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_ClearRichPresence(ref Discord_Client self);

        // --- Activity join/invite event callbacks ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_SetActivityJoinCallback(
            ref Discord_Client self,
            Discord_Client_ActivityJoinCallback cb,
            Discord_FreeFn cbUserDataFree,
            IntPtr cbUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_SetActivityInviteCreatedCallback(
            ref Discord_Client self,
            Discord_Client_ActivityInviteCallback cb,
            Discord_FreeFn cbUserDataFree,
            IntPtr cbUserData);

        // --- Discord_Activity object ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_Init(ref Discord_Activity self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_Drop(ref Discord_Activity self);

        // Required string — passed by value (Discord_String, not pointer)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetName(ref Discord_Activity self, Discord_String value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetType(ref Discord_Activity self, Discord_ActivityTypes value);

        // Optional string — nullable Discord_String* (null = clear/unset)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetState(ref Discord_Activity self, Discord_String* value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetDetails(ref Discord_Activity self, Discord_String* value);

        // Optional uint64* — null = unset
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetApplicationId(ref Discord_Activity self, ulong* value);

        // Sub-object setters — only called when sub-object has content
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetAssets(ref Discord_Activity self, ref Discord_ActivityAssets value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetTimestamps(ref Discord_Activity self, ref Discord_ActivityTimestamps value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetParty(ref Discord_Activity self, ref Discord_ActivityParty value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Activity_SetSecrets(ref Discord_Activity self, ref Discord_ActivitySecrets value);

        // --- Discord_ActivityAssets ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityAssets_Init(ref Discord_ActivityAssets self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityAssets_Drop(ref Discord_ActivityAssets self);

        // All asset text fields are optional Discord_String*
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityAssets_SetLargeImage(ref Discord_ActivityAssets self, Discord_String* value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityAssets_SetLargeText(ref Discord_ActivityAssets self, Discord_String* value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityAssets_SetSmallImage(ref Discord_ActivityAssets self, Discord_String* value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityAssets_SetSmallText(ref Discord_ActivityAssets self, Discord_String* value);

        // --- Discord_ActivityTimestamps ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityTimestamps_Init(ref Discord_ActivityTimestamps self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityTimestamps_Drop(ref Discord_ActivityTimestamps self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityTimestamps_SetStart(ref Discord_ActivityTimestamps self, ulong value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityTimestamps_SetEnd(ref Discord_ActivityTimestamps self, ulong value);

        // --- Discord_ActivityParty ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityParty_Init(ref Discord_ActivityParty self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityParty_Drop(ref Discord_ActivityParty self);

        // Party Id is required string (by value)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityParty_SetId(ref Discord_ActivityParty self, Discord_String value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityParty_SetCurrentSize(ref Discord_ActivityParty self, int value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityParty_SetMaxSize(ref Discord_ActivityParty self, int value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivityParty_SetPrivacy(ref Discord_ActivityParty self, Discord_ActivityPartyPrivacy value);

        // --- Discord_ActivitySecrets ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivitySecrets_Init(ref Discord_ActivitySecrets self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivitySecrets_Drop(ref Discord_ActivitySecrets self);

        // Join secret is required string (by value) — Spectate and Match are not supported in Social SDK
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ActivitySecrets_SetJoin(ref Discord_ActivitySecrets self, Discord_String value);

        // --- Discord_ActivityInvite accessors (read inside invite callback) ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong Discord_ActivityInvite_SenderId(ref Discord_ActivityInvite self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Discord_ActivityActionTypes Discord_ActivityInvite_Type(ref Discord_ActivityInvite self);

        // --- Discord_ClientResult accessors (read inside RichPresence callback) ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool Discord_ClientResult_Successful(ref Discord_ClientResult self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Discord_ErrorType Discord_ClientResult_Type(ref Discord_ClientResult self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_ClientResult_Error(ref Discord_ClientResult self, Discord_String* returnValue);

        // --- OAuth2 / auth ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_Authorize(
            ref Discord_Client self,
            ref Discord_AuthorizationArgs args,
            Discord_Client_AuthorizationCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_CreateAuthorizationCodeVerifier(
            ref Discord_Client self,
            out Discord_AuthorizationCodeVerifier returnValue);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_GetToken(
            ref Discord_Client self,
            ulong applicationId,
            Discord_String code,
            Discord_String codeVerifier,
            Discord_String redirectUri,
            Discord_Client_TokenExchangeCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_UpdateToken(
            ref Discord_Client self,
            Discord_AuthorizationTokenType tokenType,
            Discord_String token,
            Discord_Client_UpdateTokenCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_RefreshToken(
            ref Discord_Client self,
            ulong applicationId,
            Discord_String refreshToken,
            Discord_Client_TokenExchangeCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_RevokeToken(
            ref Discord_Client self,
            ulong applicationId,
            Discord_String token,
            Discord_Client_RevokeTokenCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool Discord_Client_IsAuthenticated(ref Discord_Client self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_Client_SetTokenExpirationCallback(
            ref Discord_Client self,
            Discord_Client_TokenExpirationCallback callback,
            Discord_FreeFn callbackUserDataFree,
            IntPtr callbackUserData);

        // --- Discord_AuthorizationArgs builders ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationArgs_Init(out Discord_AuthorizationArgs self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationArgs_Drop(ref Discord_AuthorizationArgs self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationArgs_SetClientId(ref Discord_AuthorizationArgs self, ulong value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationArgs_SetScopes(ref Discord_AuthorizationArgs self, Discord_String value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationArgs_SetCodeChallenge(
            ref Discord_AuthorizationArgs self,
            ref Discord_AuthorizationCodeChallenge value);

        // --- Discord_AuthorizationCodeVerifier accessors ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationCodeVerifier_Drop(ref Discord_AuthorizationCodeVerifier self);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationCodeVerifier_Challenge(
            ref Discord_AuthorizationCodeVerifier self,
            out Discord_AuthorizationCodeChallenge returnValue);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationCodeVerifier_Verifier(
            ref Discord_AuthorizationCodeVerifier self,
            Discord_String* returnValue);

        // --- Discord_AuthorizationCodeChallenge ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Discord_AuthorizationCodeChallenge_Drop(ref Discord_AuthorizationCodeChallenge self);
    }
}
