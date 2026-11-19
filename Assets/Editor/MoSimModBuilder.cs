#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

// Install in Assets/Editor, outside any mod assembly-definition folder.
public class MoSimModBuilder : EditorWindow
{
    string modName;
    HashSet<string> selectedDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    string[] availableDlls = new string[0];
    Vector2 dllScroll;
    bool busy;
    string status = "Builds and zips the currently selected platform.";
    static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
    string Key(string suffix) { return "MoSimModBuilder." + Root + "." + suffix; }

    [MenuItem("Tools/MoSim Mod Builder")]
    static void Open() { GetWindow<MoSimModBuilder>("MoSim Mod Builder"); }

    void OnEnable()
    {
        modName = EditorPrefs.GetString(Key("mod"), "IndianaModpack");
        selectedDlls = new HashSet<string>(EditorPrefs.GetString(Key("dlls"), "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        RefreshDlls();
        minSize = new Vector2(520, 460);
    }

    void OnGUI()
    {
        GUILayout.Label("Build + ZIP", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(busy))
        {
            modName = EditorGUILayout.TextField("Addressable group / mod", modName);
            GUILayout.Label("Select every robot DLL used by this modpack", EditorStyles.boldLabel);
            if (GUILayout.Button("Match DLL names from my existing installed mod folder"))
            {
                string folder = EditorUtility.OpenFolderPanel("Select your existing IndianaModpack folder", Root, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    selectedDlls = new HashSet<string>(Directory.GetFiles(folder, "*.dll").Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
                    SaveSelection();
                    RefreshDlls();
                }
            }
            dllScroll = EditorGUILayout.BeginScrollView(dllScroll, GUILayout.Height(150));
            foreach (string name in availableDlls.Union(selectedDlls).OrderBy(n => n))
            {
                bool selected = selectedDlls.Contains(name);
                bool next = EditorGUILayout.ToggleLeft(name + (availableDlls.Contains(name) ? "" : " (not compiled / missing)"), selected);
                if (next != selected)
                {
                    if (next) selectedDlls.Add(name); else selectedDlls.Remove(name);
                    SaveSelection();
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Refresh DLL List")) RefreshDlls();
            EditorGUILayout.LabelField("Selected DLLs", selectedDlls.Count.ToString());
            EditorGUILayout.LabelField("Active target", EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.LabelField("Output", "ModBuilds (in your project folder)");
            EditorGUILayout.HelpBox("Use your usual MoSim Addressables setup. Only your mod group should have Include in Build enabled. Restart Unity before building if your project requires it.", MessageType.Info);
            if (GUILayout.Button("Build Current Platform + Create ZIP", GUILayout.Height(36)))
            {
                EditorPrefs.SetString(Key("mod"), modName);
                SaveSelection();
                BuildZip();
            }
            if (GUILayout.Button("Open ModBuilds Folder"))
            {
                string path = Path.Combine(Root, "ModBuilds");
                Directory.CreateDirectory(path);
                EditorUtility.RevealInFinder(path);
            }
        }
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    void RefreshDlls()
    {
        string folder = Path.Combine(Root, "Library", "ScriptAssemblies");
        availableDlls = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.dll").Select(Path.GetFileName).OrderBy(n => n).ToArray() : new string[0];
    }

    void SaveSelection() { EditorPrefs.SetString(Key("dlls"), string.Join("|", selectedDlls.OrderBy(n => n).ToArray())); }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static string Platform(out string aaPlatform)
    {
        switch (EditorUserBuildSettings.activeBuildTarget)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64: aaPlatform = "Windows"; return "Windows";
            case BuildTarget.StandaloneLinux64: aaPlatform = "Linux"; return "Linux";
            case BuildTarget.StandaloneOSX: aaPlatform = "OSX"; return "MacOS";
            default: throw new InvalidOperationException("Select Windows, Linux, or macOS in Unity Build Settings first.");
        }
    }

    void BuildZip()
    {
        busy = true;
        string staging = null;
        string tempZip = null;
        BuildScriptPackedMode builder = null;
        try
        {
            Require(!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlaying,
                "Wait for Unity to finish compiling/importing, and exit Play Mode.");
            Require(!string.IsNullOrWhiteSpace(modName) && modName == Path.GetFileName(modName) &&
                modName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && modName != "." && modName != "..",
                "Enter a valid mod/group name without folder separators.");
            Require(selectedDlls.Count > 0, "Select every robot DLL, or use Match DLL names from my existing installed mod folder.");
            foreach (string dllName in selectedDlls)
                Require(dllName == Path.GetFileName(dllName) && dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase), "Invalid DLL filename: " + dllName);
            string aaPlatform;
            string platform = Platform(out aaPlatform);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Require(settings != null, "No Addressables settings found in this project.");
            var group = settings.FindGroup(modName);
            Require(group != null, "Addressable group not found: " + modName + ". Enter its exact name.");
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            Require(schema != null && schema.IncludeInBuild, "Enable Include in Build for your mod group first.");
            var otherGroups = settings.groups.Where(g => g != null && g != group &&
                g.GetSchema<BundledAssetGroupSchema>() != null && g.GetSchema<BundledAssetGroupSchema>().IncludeInBuild).ToArray();
            Require(otherGroups.Length == 0, "Disable Include in Build for these other groups first: " +
                string.Join(", ", otherGroups.Select(g => g.Name).ToArray()));
            foreach (string dllName in selectedDlls)
            {
                string dll = Path.Combine(Root, "Library", "ScriptAssemblies", dllName);
                Require(File.Exists(dll), "Mod DLL not found: " + dll + ". Check the selected names and compilation errors.");
            }

            status = "Building " + platform + "...";
            EditorUtility.DisplayProgressBar("MoSim Mod Builder", status, 0.1f);
            // Run the Default Build Script directly, without editing Addressables settings.
            builder = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            var result = builder.BuildData<AddressablesPlayerBuildResult>(new AddressablesDataBuilderInput(settings));
            Require(result != null, "Addressables returned no build result.");
            Require(string.IsNullOrEmpty(result.Error), "Addressables build failed: " + result.Error +
                "\nRestart Unity and try again before making any other changes. No new ZIP was created.");
            Require(result.AssetBundleBuildResults != null && result.AssetBundleBuildResults.Count > 0,
                "Build returned no bundles. No ZIP was created.");

            string aa = Path.Combine(Root, "Library", "com.unity.addressables", "aa", aaPlatform);
            string[] catalogFiles = { "catalog.hash", "catalog.json", "settings.json" };
            foreach (string name in catalogFiles)
                Require(File.Exists(Path.Combine(aa, name)), "Required file not found: " + Path.Combine(aa, name) +
                    ". This tool expects the JSON catalog layout from the MoSim instructions.");
            string output = Path.Combine(Root, "ModBuilds");
            Directory.CreateDirectory(output);
            string release = modName + "_" + platform;
            staging = Path.Combine(output, ".staging_" + Guid.NewGuid().ToString("N"));
            string package = Path.Combine(staging, modName);
            Directory.CreateDirectory(package);
            foreach (var bundle in result.AssetBundleBuildResults)
            {
                string source = Path.GetFullPath(Path.IsPathRooted(bundle.FilePath) ? bundle.FilePath : Path.Combine(Root, bundle.FilePath));
                Require(File.Exists(source), "Built bundle missing: " + source);
                string destination = Path.Combine(package, Path.GetFileName(source));
                Require(!File.Exists(destination), "Duplicate bundle filename: " + Path.GetFileName(source));
                File.Copy(source, destination);
            }
            foreach (string name in catalogFiles) File.Copy(Path.Combine(aa, name), Path.Combine(package, name));
            foreach (string dllName in selectedDlls)
                File.Copy(Path.Combine(Root, "Library", "ScriptAssemblies", dllName), Path.Combine(package, dllName));
            EditorUtility.DisplayProgressBar("MoSim Mod Builder", "Creating ZIP...", 0.85f);
            tempZip = Path.Combine(output, ".pending_" + Guid.NewGuid().ToString("N") + ".zip");
            // ZIP contains the exact group-name folder, as required by MoSim.
            using (var stream = new FileStream(tempZip, FileMode.CreateNew))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                foreach (string file in Directory.GetFiles(package))
                {
                    var entry = zip.CreateEntry(modName + "/" + Path.GetFileName(file), System.IO.Compression.CompressionLevel.Optimal);
                    using (var source = File.OpenRead(file))
                    using (var target = entry.Open()) source.CopyTo(target);
                }
            string finalZip = Path.Combine(output, release + ".zip");
            if (File.Exists(finalZip))
            {
                string backups = Path.Combine(output, "PreviousBuilds");
                Directory.CreateDirectory(backups);
                string backup = Path.Combine(backups, release + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".zip");
                File.Replace(tempZip, finalZip, backup);
            }
            else File.Move(tempZip, finalZip);
            tempZip = null;
            status = "Ready: " + Path.GetFileName(finalZip);
            Debug.Log("MoSim mod ZIP created: " + finalZip);
            EditorUtility.RevealInFinder(finalZip);
        }
        catch (Exception exception)
        {
            status = "Failed. See the Console for details. Earlier ZIPs were preserved.";
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("MoSim Mod Builder", exception.Message, "OK");
        }
        finally
        {
            if (builder != null) DestroyImmediate(builder);
            EditorUtility.ClearProgressBar();
            busy = false;
            try
            {
                if (tempZip != null && File.Exists(tempZip)) File.Delete(tempZip);
                if (staging != null && Directory.Exists(staging)) Directory.Delete(staging, true);
            }
            catch (Exception cleanupError) { Debug.LogWarning("Temporary build cleanup failed: " + cleanupError.Message); }
            Repaint();
        }
    }
}
#endif
