using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DiscordCore
{
    public struct ModState
    {
        public bool Active;
        public int Priority;
    }

    [Serializable]
    public class DiscordTokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public long TokenExpiry { get; set; } = 0;

        [JsonIgnore]
        public bool HasValidToken =>
            !string.IsNullOrEmpty(AccessToken) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < TokenExpiry - 60;

        [JsonIgnore]
        public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);
    }

    [Serializable]
    public class Config
    {
        private bool _enableDiscordCore;
        public bool EnableDiscordCore
        {
            get { return _enableDiscordCore; }
            set
            {
                _enableDiscordCore = value;
                Save();
            }
        }

        private bool _allowJoin;
        public bool AllowJoin
        {
            get { return _allowJoin; }
            set
            {
                _allowJoin = value;
                Save();
            }
        }

        private bool _allowSpectate;
        public bool AllowSpectate
        {
            get { return _allowSpectate; }
            set
            {
                _allowSpectate = value;
                Save();
            }
        }

        private bool _allowInvites;
        public bool AllowInvites
        {
            get { return _allowInvites; }
            set
            {
                _allowInvites = value;
                Save();
            }
        }

        public Dictionary<string, DiscordTokenData> AuthTokens { get; set; }

        // Returns the stored token data for the given app ID, or null if none exists.
        public DiscordTokenData GetTokens(long appId)
        {
            DiscordTokenData data;
            AuthTokens.TryGetValue(appId.ToString(), out data);
            return data;
        }

        public void SaveAuthTokens(long appId, string accessToken, string refreshToken, long expiry)
        {
            string key = appId.ToString();
            DiscordTokenData data;
            if (!AuthTokens.TryGetValue(key, out data))
            {
                data = new DiscordTokenData();
                AuthTokens[key] = data;
            }
            data.AccessToken = accessToken;
            data.RefreshToken = refreshToken;
            data.TokenExpiry = expiry;
            Save();
        }

        public void ClearAuthTokens(long appId)
        {
            AuthTokens.Remove(appId.ToString());
            Save();
        }

        public Dictionary<string, ModState> ModStates { get; set; }

        Config()
        {
            _enableDiscordCore = true;
            _allowJoin = true;
            _allowSpectate = true;
            _allowInvites = true;

            AuthTokens = new Dictionary<string, DiscordTokenData>();
            ModStates = new Dictionary<string, ModState>();
        }

        private static Config _instance;

        private static FileInfo FileLocation { get; } = new FileInfo($"./UserData/{Assembly.GetExecutingAssembly().GetName().Name}.json");

        public static Config Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Config();
                return _instance;
            }
        }
        

        public static bool Create()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation.Directory.Create();
                Plugin.log.Info($"Creating new config @ {FileLocation.FullName}");
                Instance.Save();
            }
            catch (Exception)
            {
                Plugin.log.Error($"Unable to create new config @ {FileLocation.FullName}");
                return false;
            }
            return true;
        }

        public static bool Load()
        {
            if (_instance != null) return false;
            try
            {
                FileLocation.Directory.Create();

                if (File.Exists(FileLocation.FullName))
                {
                    Plugin.log.Debug($"Attempting to load JSON @ {FileLocation.FullName}");
                    _instance = JsonConvert.DeserializeObject<Config>(File.ReadAllText(FileLocation.FullName));

                    _instance.Save();
                }
                else
                    Create();
            }
            catch (Exception)
            {
                Plugin.log.Error($"Unable to load config @ {FileLocation.FullName}");
                return false;
            }
            return true;
        }

        public bool Save()
        {
            try
            {
                using (var f = new StreamWriter(FileLocation.FullName))
                {
                    Plugin.log.Debug($"Writing to File @ {FileLocation.FullName}");
                    var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    f.Write(json);
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.log.Critical(ex);
                return false;
            }
        }
    }
}
