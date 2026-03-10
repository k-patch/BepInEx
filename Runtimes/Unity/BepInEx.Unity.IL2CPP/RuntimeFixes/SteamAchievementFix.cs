using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Hook;

namespace BepInEx.IL2CPP.RuntimeFixes;

/// <summary>
/// Fixes the issue where Steam achievements do not work in the BepInEx IL2CPP environment.
///
/// Root Cause:
///   Facepunch.Steamworks (or Steamworks.NET) calls the native function 'SteamAPI_RestartAppIfNecessary'
///   at game startup to verify if the game was launched correctly through the Steam client.
///   Because BepInEx's Doorstop hooks the process and injects itself,
///   Steam recognizes this process as a "directly executed process" and returns true,
///   causing the game to attempt a restart through Steam or abort Steam API initialization.
///   As a result, Steam integration features such as achievements and statistics are disabled.
///
/// Solution:
///   This fix directly hooks the native function 'SteamAPI_RestartAppIfNecessary' and
///   forces it to always return 0 (false). This allows the game to run normally while
///   maintaining its Steam integration state.
/// </summary>
internal static class SteamAchievementFix
{
    // SteamAPI_RestartAppIfNecessary function signature:
    //   bool SteamAPI_RestartAppIfNecessary(uint unAppID)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RestartAppIfNecessaryDelegate(uint unAppID);


    private static RestartAppIfNecessaryDelegate _originalDelegate;
    private static GCHandle _delegateHandle;
    private static IntPtr _originalFunctionPtr;

    private static bool _applied;

    /// <summary>
    /// Must be called after the Steam library is loaded.
    /// Ideally applied just before IL2CPPChainloader loads GameAssembly.dll.
    /// </summary>
    public static void Apply()
    {
        if (_applied) return;

        try
        {
            // Steam API library name varies by platform.
            // Windows: steam_api64.dll / steam_api.dll
            // Linux:   libsteam_api.so
            // macOS:   libsteam_api.dylib
            var steamLibName = GetSteamLibraryName();
            if (steamLibName == null)
            {
                Logger.Log(LogLevel.Debug, "[SteamFix] Unsupported platform - skipping Steam fix.");
                return;
            }

            // Attempt to load the Steam library (returns existing handle if already loaded)
            if (!NativeLibrary.TryLoad(steamLibName, out var steamHandle))
            {
                Logger.Log(LogLevel.Debug, $"[SteamFix] Failed to load '{steamLibName}' - Steam might not be used by this game or the path may be different.");
                return;
            }

            // Get the symbol address for SteamAPI_RestartAppIfNecessary
            if (!NativeLibrary.TryGetExport(steamHandle, "SteamAPI_RestartAppIfNecessary", out var funcPtr))
            {
                Logger.Log(LogLevel.Debug, "[SteamFix] 'SteamAPI_RestartAppIfNecessary' symbol not found - skipping Steam fix.");
                return;
            }

            _originalFunctionPtr = funcPtr;

            // Create the patch delegate and pin it to prevent GC collection
            RestartAppIfNecessaryDelegate patchedDelegate = PatchedRestartAppIfNecessary;
            _delegateHandle = GCHandle.Alloc(patchedDelegate);

            // Utilize the same INativeDetour interface as IL2CPPChainloader.
            var nativeDetour = INativeDetour.CreateAndApply(
                funcPtr,
                patchedDelegate,
                out _originalDelegate
            );

            _applied = true;
            Logger.Log(LogLevel.Debug, $"[SteamFix] SteamAPI_RestartAppIfNecessary (0x{funcPtr.ToInt64():X}) patched - achievements enabled");
        }
        catch (Exception ex)
        {
            // Can fail normally in games that do not use Steam, so treat as Debug level
            Logger.Log(LogLevel.Debug, $"[SteamFix] Exception occurred while applying patch (game might not use Steam): {ex.Message}");
        }
    }

    /// <summary>
    /// Always returns 0 (false) so the game gives up on restarting through Steam
    /// and maintains the current Steam API connection state.
    /// </summary>
    private static int PatchedRestartAppIfNecessary(uint unAppID)
    {
        Logger.Log(LogLevel.Debug, $"[SteamFix] SteamAPI_RestartAppIfNecessary(AppID={unAppID}) call blocked -> returning false (achievements maintained)");
        return 0; // false: "Restart not required, current environment is fine"
    }

    private static string GetSteamLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.Is64BitProcess ? "steam_api64" : "steam_api";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "libsteam_api";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libsteam_api";
        return null;
    }
}
