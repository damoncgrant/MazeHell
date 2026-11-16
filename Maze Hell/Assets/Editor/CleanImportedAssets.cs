using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Maze Hell > Clean Unused Imported Assets
//
// Keeps everything that your scenes (Assets/Scenes) actually use from the imported packs,
// e.g. the TerrainMesh010 prefab and the Door_14_brown prefab plus their meshes, materials,
// textures and shaders, and moves every other file in the pack folders to the Recycle Bin.
// Nothing outside the folders listed in CleanupFolders is touched.
public static class CleanImportedAssets
{
    private static readonly string[] CleanupFolders =
    {
        "Assets/ImportedAssets",
        "Assets/_TerrainAutoUpgrade",
    };

    private const string ScenesFolder = "Assets/Scenes";

    [MenuItem("Tools/Maze Hell/Clean Unused Imported Assets")]
    public static void Run()
    {
        // Make sure the latest scene changes are on disk, so we see what the scenes really use.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Clean Imported Assets",
                $"No scenes found in {ScenesFolder}. Nothing was deleted.", "OK");
            return;
        }

        // Everything the scenes depend on, recursively (prefabs -> meshes -> materials -> textures -> shaders).
        var keep = new HashSet<string>(AssetDatabase.GetDependencies(scenes, true));

        string[] folders = CleanupFolders.Where(AssetDatabase.IsValidFolder).ToArray();
        var allInPacks = AssetDatabase.FindAssets("", folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .Distinct()
            .ToList();

        var kept = allInPacks.Where(keep.Contains).OrderBy(p => p).ToList();
        var remove = allInPacks.Where(p => !keep.Contains(p)).OrderBy(p => p).ToList();

        if (remove.Count == 0)
        {
            EditorUtility.DisplayDialog("Clean Imported Assets", "Nothing to remove; every pack file is in use.", "OK");
            return;
        }

        long bytes = remove.Sum(p => File.Exists(p) ? new FileInfo(p).Length : 0);
        var keptPrefabs = kept.Where(p => p.EndsWith(".prefab")).Select(Path.GetFileNameWithoutExtension).ToList();

        var report = new StringBuilder();
        report.AppendLine($"[CleanImportedAssets] Keeping {kept.Count} files:");
        kept.ForEach(p => report.AppendLine("  KEEP   " + p));
        report.AppendLine($"[CleanImportedAssets] Removing {remove.Count} files:");
        remove.ForEach(p => report.AppendLine("  REMOVE " + p));
        Debug.Log(report.ToString());

        string message =
            $"Scenes checked: {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}\n\n" +
            $"Prefabs kept: {(keptPrefabs.Count > 0 ? string.Join(", ", keptPrefabs) : "(none!)")}\n" +
            $"Files kept: {kept.Count}\n\n" +
            $"Files to move to the Recycle Bin: {remove.Count} ({bytes / (1024f * 1024f):0.#} MB)\n\n" +
            "The full list is in the Console. Continue?";

        if (!EditorUtility.DisplayDialog("Clean Imported Assets", message, "Move to Recycle Bin", "Cancel"))
            return;

        var failed = new List<string>();
        AssetDatabase.MoveAssetsToTrash(remove.ToArray(), failed);
        DeleteEmptyFolders(folders);
        AssetDatabase.Refresh();

        Debug.Log($"[CleanImportedAssets] Done. Moved {remove.Count - failed.Count} files to the Recycle Bin." +
                  (failed.Count > 0 ? $" Failed: {string.Join(", ", failed)}" : ""));
    }

    private static void DeleteEmptyFolders(IEnumerable<string> roots)
    {
        foreach (string root in roots)
        {
            if (!Directory.Exists(root)) continue;

            // Deepest folders first, so parents empty out as we go.
            var dirs = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                .Append(root)
                .OrderByDescending(d => d.Length);

            foreach (string dir in dirs)
            {
                bool empty = !Directory.EnumerateFiles(dir).Any(f => !f.EndsWith(".meta"))
                             && !Directory.EnumerateDirectories(dir).Any();
                if (empty)
                    AssetDatabase.DeleteAsset(dir.Replace('\\', '/'));
            }
        }
    }
}
