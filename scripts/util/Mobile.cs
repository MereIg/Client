using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

/// <summary>
/// Helpers for running Rhythia on Android phones (touch input, scaling, storage access).
/// Everything here is a no-op on desktop.
/// </summary>
public static class Mobile
{
    public const string STORAGE_PERMISSION = "android.permission.MANAGE_EXTERNAL_STORAGE";
    public const string DOWNLOADS_FOLDER = "/storage/emulated/0/Download";

    /// <summary>The UI is laid out for a 720px tall window; phones get scaled up to match.</summary>
    private const float design_height = 720f;

    public static bool IsAndroid { get; } = OS.GetName() == "Android";

    /// <summary>Call once at startup.</summary>
    public static void Initialize(Node node)
    {
        if (!IsAndroid)
            return;

        var root = node.GetTree().Root;

        applyScale(root);
        root.SizeChanged += () => applyScale(root);

        // Keep the screen awake while playing
        DisplayServer.ScreenSetKeepOn(true);
    }

    private static void applyScale(Window root)
    {
        float height = Mathf.Min(root.Size.X, root.Size.Y);

        root.ContentScaleFactor = Mathf.Max(1f, height / design_height);
    }

    public static bool HasStorageAccess() =>
        !IsAndroid || OS.GetGrantedPermissions().Contains(STORAGE_PERMISSION);

    /// <summary>
    /// Opens the Android "All files access" screen so maps can be read from Downloads.
    /// </summary>
    public static void RequestStorageAccess()
    {
        if (HasStorageAccess())
            return;

        OS.RequestPermission(STORAGE_PERMISSION);
    }

    private static bool scanning = false;

    /// <summary>
    /// Imports any new map files (.sspm/.phxm/.rhm) from the phone's Downloads folder,
    /// so maps downloaded in the browser show up in-game automatically.
    /// </summary>
    public static async void ImportFromDownloads()
    {
        if (!IsAndroid || scanning || MapParser.Instance == null || !HasStorageAccess() || SceneManager.Scene is Game)
            return;

        scanning = true;

        try
        {
            string recordPath = $"{Constants.USER_FOLDER}/android_imported.txt";
            HashSet<string> seen = File.Exists(recordPath) ? File.ReadAllLines(recordPath).ToHashSet() : [];
            var fresh = new List<string>();
            var newKeys = new List<string>();

            foreach (string folder in new[] { DOWNLOADS_FOLDER, $"{DOWNLOADS_FOLDER}/Rhythia" })
            {
                if (!Directory.Exists(folder))
                    continue;

                foreach (string file in Directory.EnumerateFiles(folder))
                {
                    string ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

                    // .txt is skipped here because Downloads is full of unrelated text files
                    if (ext is not ("sspm" or "phxm" or "rhm"))
                        continue;

                    var info = new FileInfo(file);
                    string key = $"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";

                    if (seen.Contains(key))
                        continue;

                    fresh.Add(file);
                    newKeys.Add(key);
                }
            }

            if (fresh.Count == 0)
                return;

            await MapParser.BulkImport(fresh.ToArray(), true);

            File.AppendAllLines(recordPath, newKeys);
        }
        catch (System.Exception exception)
        {
            Logger.Error(exception);
        }
        finally
        {
            scanning = false;
        }
    }

    /// <summary>Simulates a key tap, so touch gestures can reuse the game's keyboard shortcuts.</summary>
    public static void SendKey(Key key, bool pressed)
    {
        var keyEvent = new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed,
        };

        Input.ParseInputEvent(keyEvent);
    }

    public static void TapKey(Key key)
    {
        SendKey(key, true);
        SendKey(key, false);
    }
}
