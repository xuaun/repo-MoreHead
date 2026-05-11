// One-time migration: copies bridge cosmetic PNGs that were previously saved to
// the vanilla cache path ( ...\Cache\Icons\Cosmetics\ ) over to our private
// path ( ...\MoreHeadBridge_Icons\ ). Without this, the icons the user spent
// time hovering/generating get wiped by REPOLib at the next launch and would
// have to be regenerated from scratch.
//
// Safe to keep running every launch: it only copies, never deletes; and it
// skips files that already exist at the destination. Once everything is
// migrated the old vanilla-cache PNGs will be auto-deleted by REPOLib itself.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreHeadBridge;

internal static class IconCacheMigration
{
    internal static void Run()
    {
        try
        {
            string oldDir = Path.Combine(
                Application.persistentDataPath, "Cache", "Icons", "Cosmetics");
            string newDir = IconCapture.CacheDir;

            if (!Directory.Exists(oldDir)) return;

            // Build a set of "<lowercase internal name>.png" for every registered bridge
            // cosmetic, so we only migrate files that actually belong to us.
            var bridgeFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in HhhCosmeticLoader.RegisteredAssetIds)
            {
                int colon = id.IndexOf(':');
                if (colon < 0) continue;
                bridgeFileNames.Add(id.Substring(colon + 1) + ".png");
            }
            if (bridgeFileNames.Count == 0) return;

            Directory.CreateDirectory(newDir);

            int migrated = 0;
            int skipped  = 0;

            foreach (string oldFile in Directory.GetFiles(oldDir, "*.png"))
            {
                string filename = Path.GetFileName(oldFile);
                if (!bridgeFileNames.Contains(filename)) continue;

                string newFile = Path.Combine(newDir, filename);
                if (File.Exists(newFile)) { skipped++; continue; }

                try
                {
                    File.Copy(oldFile, newFile);
                    migrated++;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogDebug($"[MoreHeadBridge] Could not migrate '{filename}': {ex.Message}");
                }
            }

            if (migrated > 0 || skipped > 0)
                Plugin.Logger.LogInfo(
                    $"[MoreHeadBridge] Icon cache migration: {migrated} copied, {skipped} already present.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[MoreHeadBridge] Icon cache migration failed: {ex.Message}");
        }
    }
}
