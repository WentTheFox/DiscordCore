# DiscordCore

A utility mod for easily doing Discord management and handling priority between rich presence systems.

Requires:
 * BSIPA 4
 * [BeatSaberMarkupLanguage](https://github.com/monkeymanboy/BeatSaberMarkupLanguage)
 * DiscordSocialSDK 1.9 (provided in the release zip)

## Usage

### Setting rich presence

```csharp
using Discord;
using DiscordCore;

// In your plugin's Init or OnEnable:
DiscordInstance _discord = DiscordManager.instance.CreateInstance(new DiscordSettings
{
    modId         = "MyMod",
    modName       = "My Mod",
    appId         = 123456789012345678L,  // your Discord application ID
    handleInvites = false,
});

// Update the presence whenever your game state changes:
_discord.UpdateActivity(new Activity
{
    Name    = "Beat Saber",
    Details = "Playing a song",
    State   = "Expert+",
    Timestamps = new ActivityTimestamps
    {
        Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    },
    Assets = new ActivityAssets
    {
        LargeImage = "cover_art",
        LargeText  = "Song Title",
    },
});

// Clear presence when done (e.g. back in the menu):
_discord.ClearActivity();

// Remove your instance when your plugin unloads:
_discord.DestroyInstance();
```

### Handling join invites

Set `handleInvites = true` in `DiscordSettings` and subscribe to the instance events:

```csharp
DiscordInstance _discord = DiscordManager.instance.CreateInstance(new DiscordSettings
{
    modId         = "MyMod",
    modName       = "My Mod",
    appId         = 123456789012345678L,
    handleInvites = true,
});

_discord.OnActivityJoin += OnJoin;
_discord.OnActivityJoinRequest += OnJoinRequest;
_discord.OnActivityInvite += OnInvite;

private void OnJoin(string secret)
{
    // User accepted a join invite, secret is your join secret string
}

private void OnJoinRequest(ref User user)
{
    // Another user asked to join, user.Id is their Discord user ID
    // (only Id is populated; Username/Avatar are not available via the Social SDK)
}

private void OnInvite(ActivityActionType type, ref User user, ref Activity activity)
{
    // An incoming join invite arrived from user.Id
}
```

Include your join secret in the activity so Discord can send it:

```csharp
_discord.UpdateActivity(new Activity
{
    // ...
    Party = new ActivityParty
    {
        Id   = "lobby-guid",
        Size = new ActivityPartySize { CurrentSize = 1, MaxSize = 5 },
    },
    Secrets = new ActivitySecrets
    {
        Join = "your-join-secret",
    },
});
```

### Priority

When multiple mods have registered instances, the one with the lowest `Priority` value is shown.
Priority is assigned automatically in registration order. You can adjust it via the in-game
DiscordCore settings menu, and the value is persisted in the config.

---

## Breaking changes in v4.0.0

v4.0.0 migrates from the legacy Discord Game SDK to the Discord Social SDK. If your mod depends on DiscordCore, the following changes affect you.

### `DiscordClient.DefaultAppID` removed

There is no longer a built-in fallback app ID. Every mod must supply an explicit `appId` in
`DiscordSettings`. Passing `0` or a negative value is treated as "no app ID", DiscordCore will
not connect until at least one instance with a valid app ID is registered.

The connection to Discord is now deferred until the first game update tick after all mods have
loaded, so there is no longer an initial connection with a wrong app ID that gets replaced later.

### Removed types and members

| Removed | Notes |
|---------|-------|
| `DiscordClient.DefaultAppID` | No replacement, each mod must supply its own app ID |

### Unchanged public API

Everything else is backward-compatible. `Discord.Activity`, `Discord.User`, all event delegate
types (`ActivityJoinHandler`, `ActivityInviteHandler`, etc.), `DiscordInstance.UpdateActivity()`,
`DiscordInstance.ClearActivity()`, and all `DiscordInstance` events have the same signatures as
before.

The `Discord.Result`, `Discord.ResultException`, and `Discord.ActivityActionType.Spectate` stubs
are still present for source compatibility, but spectate is not supported by the Social SDK and
the corresponding event will never fire.

---

## Credits

* [@FizzyApple12](https://github.com/FizzyApple12) - original author
* [@qe201020335](https://github.com/qe201020335) - BSMT migration
* [@WentTheFox](https://github.com/WentTheFox) - Maintenance, Discord Social SDK migration

## For developers
### Building DiscordCore
- Download [Discord Social SDK v1.9](https://discord.com/developers/discord-social-sdk/overview) and extract `discord_partner_sdk.dll` (from `bin/release`) into `Refs/Libs/Native`
- Create `DiscordCore/DiscordCore.csproj.user` and add your Beat Saber game path to it

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
   <PropertyGroup>
      <!-- Change this path to your Beat Saber install path -->
      <BeatSaberDir>U:/SteamLibrary/steamapps/common/Beat Saber</BeatSaberDir>
   </PropertyGroup>
</Project>
```

- The compiled binaries and all related files will be automatically copied into your game upon build
