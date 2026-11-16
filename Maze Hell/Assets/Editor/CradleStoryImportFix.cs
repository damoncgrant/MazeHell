using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Cradle.Editor;
using UnityEditor;
using UnityEngine;

// Workaround for Cradle on Unity 6.
//
// Cradle's own importer generates the story's C# code fine, but then test-compiles it against every
// assembly loaded in the editor. On Unity 6 that includes both mscorlib and the .NET Standard facades,
// so the test compile fails with "type is defined multiple times" and Cradle refuses to write the .cs file.
//
// This script runs the same importer steps (parse, transcode, fill Story.template) but skips that broken
// test compile and just writes the .cs file. Unity compiles it normally afterwards, so any real errors in
// the story still show up in the Console.
//
// It runs automatically when a .twee/.html story is imported, and can be run by hand from
// Tools > Maze Hell > Reimport Twine Stories (or right-click a story file > Cradle: Generate Story Script).
public class CradleStoryImportFix : AssetPostprocessor
{
    static readonly Regex NameSanitizer = new Regex(@"([^a-z0-9_]|^[0-9])", RegexOptions.IgnoreCase);

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (IsStoryFile(path))
                // Run after Cradle's own (failing) postprocessor has finished.
                EditorApplication.delayCall += () => Generate(path);
        }
    }

    [MenuItem("Tools/Maze Hell/Reimport Twine Stories")]
    static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsStoryFile(path) && !path.Contains("/Cradle/"))
                Generate(path);
        }
    }

    [MenuItem("Assets/Cradle: Generate Story Script")]
    static void GenerateSelected()
    {
        foreach (var obj in Selection.objects)
            Generate(AssetDatabase.GetAssetPath(obj));
    }

    [MenuItem("Assets/Cradle: Generate Story Script", true)]
    static bool GenerateSelectedValidate()
    {
        return Selection.objects.Any(o => IsStoryFile(AssetDatabase.GetAssetPath(o)));
    }

    static bool IsStoryFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return (ext == ".twee" || ext == ".html") && !path.Contains("/Cradle/");
    }

    static void Generate(string assetPath)
    {
        string fileNameExt = Path.GetFileName(assetPath);
        string storyName = Path.GetFileNameWithoutExtension(assetPath);
        if (NameSanitizer.Replace(storyName, string.Empty) != storyName)
        {
            Debug.LogError($"[CradleFix] '{storyName}' is not a valid C# class name (no spaces or symbols).");
            return;
        }

        // Find Cradle's importer for this extension (registered in CradleAssetProcessor.ImporterTypes).
        var importerTypes = (Dictionary<string, Type>)typeof(CradleAssetProcessor)
            .GetField("ImporterTypes", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);

        string ext = Path.GetExtension(assetPath).Substring(1);
        if (!importerTypes.TryGetValue(ext, out Type importerType))
        {
            Debug.LogError($"[CradleFix] Cradle has no importer for .{ext} files.");
            return;
        }

        var importer = (StoryImporter)Activator.CreateInstance(importerType);
        SetProperty(importer, "AssetPath", assetPath);

        // CSharpCodeProvider is created by reflection so this script doesn't need a System.CodeDom reference.
        Type providerType = Type.GetType("Microsoft.CSharp.CSharpCodeProvider, System");
        var provider = (IDisposable)Activator.CreateInstance(providerType);
        SetProperty(importer, "CodeDomProvider", provider);

        try
        {
            if (!importer.IsAssetRelevant())
                return;

            importer.Initialize();
            importer.Transcode();

            StoryFormatMetadata meta = importer.Metadata;
            var passages = importer.Passages.Select(p => new CradleAssetProcessor.TemplatePassageData
            {
                Pid = p.Pid,
                Name = p.Name.Replace("\"", "\"\""),
                Tags = p.Tags,
                Code = SplitLines(p.Code.Main),
                Fragments = p.Code.Fragments.Select((frag, i) => new CradleAssetProcessor.TemplatePassageFragment
                {
                    Pid = p.Pid,
                    FragId = i,
                    Code = SplitLines(frag)
                }).ToArray()
            }).ToArray();

            string templatePath = AssetDatabase.FindAssets("Story")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("/Story.template"));
            if (templatePath == null)
            {
                Debug.LogError("[CradleFix] Couldn't find Cradle's Story.template.");
                return;
            }

            string output = Nustache.Core.Render.FileToString(Path.GetFullPath(templatePath), new Dictionary<string, object>
            {
                { "version", typeof(CradleAssetProcessor).Assembly.GetName().Version.ToString() },
                { "originalFile", fileNameExt },
                { "storyFormatName", meta.StoryFormatName },
                { "storyFormatNamespace", meta.StoryBaseType.Namespace },
                { "storyFormatClass", meta.StoryBaseType.FullName },
                { "storyName", storyName },
                { "startPassage", meta.StartPassage ?? "Start" },
                { "vars", importer.Vars },
                { "macroLibs", importer.MacroLibs },
                { "strictMode", meta.StrictMode ? "true" : "false" },
                { "passages", passages }
            });

            // Same cleanup Cradle does before saving.
            output = Regex.Replace(output, @"^\s*\#line\b.*$", string.Empty, RegexOptions.Multiline);

            // Cradle writes the class as "@LabyrinthStory". C# accepts that, but Unity 6 reads the "@" as part of
            // the class name, so it no longer matches the file name and the component "can't be found".
            output = output.Replace("class @" + storyName + ":", "class " + storyName + " :")
                           .Replace("@" + storyName + "()", storyName + "()");

            string csFile = Path.Combine(Path.GetDirectoryName(assetPath), storyName + ".cs").Replace('\\', '/');
            if (File.Exists(csFile) && File.ReadAllText(csFile) == output)
                return; // unchanged, avoid a needless recompile

            File.WriteAllText(csFile, output, Encoding.UTF8);
            AssetDatabase.ImportAsset(csFile, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[CradleFix] Generated {csFile} from {fileNameExt} ({passages.Length} passages). " +
                      "You can ignore Cradle's 'has some errors and can't be imported' message above.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CradleFix] Importing {fileNameExt} failed: {ex.GetBaseException().Message}\n{ex}");
        }
        finally
        {
            provider.Dispose();
        }
    }

    static string[] SplitLines(string code)
    {
        return (code ?? string.Empty).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
    }

    static void SetProperty(object target, string name, object value)
    {
        PropertyInfo prop = typeof(StoryImporter).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        prop.GetSetMethod(true).Invoke(target, new[] { value });
    }
}
