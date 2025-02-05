using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Common.Data;
using Common.Data.Attributes;
using Game.Editor.CodeGen;
using UnityEditor.Compilation;
using AssetBuilder.Editor;

namespace Game.Editor
{

    public class AssetBuilderWindow : EditorWindow
    {
        private string assetOutputPath = "Assets/Resources/GameData";
        private Vector2 scrollPosition;
        private Dictionary<string, bool> buildToggles = new Dictionary<string, bool>();
        private const string GENERATED_NAMESPACE = "Game.Assets.Generated";

        // Cache for created assets to handle references
        private Dictionary<object, string> assetPathCache = new Dictionary<object, string>();

        [MenuItem("Tools/Asset Builder")]
        public static void ShowWindow()
        {
            GetWindow<AssetBuilderWindow>("Asset Builder");
        }

        private void OnEnable()
        {
            RefreshBuildToggles();
            // Clear the provider cache when the window is opened
            GameDataProviderUtils.ClearCache();
        }


        private void RefreshBuildToggles()
        {
            try
            {
                var commonAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetTypes()
                        .Any(t => t.Namespace?.StartsWith("Common.Data") == true));

                if (commonAssembly == null)
                {
                    Debug.LogWarning("Could not find assembly containing Common.Data namespace.");
                    return;
                }

                var types = commonAssembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<GenerateAssetAttribute>() != null);

                buildToggles.Clear();
                foreach (var type in types)
                {
                    buildToggles[type.Name] = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error refreshing build toggles: {e.Message}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Asset Builder", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            assetOutputPath = EditorGUILayout.TextField("Output Path", assetOutputPath);

            EditorGUILayout.Space();

            // Add Recompile button in a horizontal group with Generate Asset Classes
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Asset Classes"))
            {
                AssetClassGenerator.GenerateAssetClasses();
                EditorUtility.DisplayDialog("Asset Generator",
                    "Asset classes generated successfully! Please wait for Unity to recompile.", "OK");
            }

            if (GUILayout.Button("Force Recompile"))
            {
                ForceRecompile();
            }

            EditorGUILayout.EndHorizontal();

            // Add a help box explaining the Force Recompile button
            EditorGUILayout.HelpBox(
                "If changes are not detected after generating asset classes, use 'Force Recompile' to manually trigger a script compilation.",
                MessageType.Info);

            EditorGUILayout.Space();

            if (buildToggles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No types found with GenerateAsset attribute. Make sure your data classes are properly compiled and have the [GenerateAsset] attribute.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Build Options", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                foreach (var toggle in buildToggles.ToList())
                {
                    buildToggles[toggle.Key] = EditorGUILayout.Toggle($"Build {toggle.Key}s", toggle.Value);
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(buildToggles.Count == 0);
            if (GUILayout.Button("Build Assets"))
            {
                BuildAssets();
            }

            EditorGUILayout.Space();

            // Add Clean Assets button with red tint
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f); // Light red
            if (GUILayout.Button("Clean Assets"))
            {
                CleanGeneratedAssets();
            }
            GUI.backgroundColor = originalColor;

            // Add a help box explaining the Clean Assets button
            EditorGUILayout.HelpBox(
                "The Clean Assets button will remove all generated assets from the output folder. This action cannot be undone.",
                MessageType.Warning);

            EditorGUILayout.Space();

            EditorGUI.EndDisabledGroup();
        }

        private void CleanGeneratedAssets()
        {
            try
            {
                if (!Directory.Exists(assetOutputPath))
                {
                    EditorUtility.DisplayDialog("Clean Assets",
                        "No assets folder found. Nothing to clean.", "OK");
                    return;
                }

                // Ask for confirmation
                bool shouldProceed = EditorUtility.DisplayDialog("Clean Assets",
                    "This will delete all generated assets in the output folder. This action cannot be undone. Are you sure you want to proceed?",
                    "Yes, Clean Assets", "Cancel");

                if (!shouldProceed)
                    return;

                // Get all asset files in the output directory and its subdirectories
                string[] assetFiles = Directory.GetFiles(assetOutputPath, "*.asset", SearchOption.AllDirectories);
                int count = 0;

                foreach (string assetPath in assetFiles)
                {
                    // Convert to Unity path format
                    string unityPath = assetPath.Replace('\\', '/');
                    if (AssetDatabase.DeleteAsset(unityPath))
                        count++;
                }

                // Try to delete empty subdirectories
                foreach (string dir in Directory.GetDirectories(assetOutputPath))
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }

                // Refresh the asset database
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Clean Assets",
                    $"Successfully cleaned {count} assets.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cleaning assets: {e.Message}");
                EditorUtility.DisplayDialog("Error",
                    $"Error cleaning assets: {e.Message}", "OK");
            }
        }

        private void BuildAssets()
        {
            try
            {
                // Clear the asset path cache
                assetPathCache.Clear();

                if (!Directory.Exists(assetOutputPath))
                {
                    Directory.CreateDirectory(assetOutputPath);
                }

                var commonAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetTypes()
                        .Any(t => t.Namespace?.StartsWith("Common.Data") == true));

                if (commonAssembly == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "Could not find assembly containing Common.Data namespace.", "OK");
                    return;
                }

                var dataTypes = commonAssembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<GenerateAssetAttribute>() != null)
                    .Where(t => !t.IsAbstract) // Skip abstract types
                    .ToList();

                // Sort types by dependencies
                var sortedTypes = SortTypesByDependencies(dataTypes);
                Debug.Log($"Building assets in order: {string.Join(", ", sortedTypes.Select(t => t.Name))}");

                // First pass: Create all assets without setting references
                foreach (var dataType in sortedTypes)
                {
                    if (buildToggles[dataType.Name])
                    {
                        CreateAssetsForType(dataType);
                    }
                }

                // Save all created assets
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Second pass: Update references for all assets
                foreach (var dataType in sortedTypes)
                {
                    if (buildToggles[dataType.Name])
                    {
                        UpdateReferencesForType(dataType);
                    }
                }

                // Final save
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Asset Builder", "Assets built successfully!", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Error building assets: {e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        private void UpdateReferencesForType(Type dataType)
        {
            try
            {
                Debug.Log($"Updating references for type: {dataType.Name}");

                // Use the new utility class to find the data array
                var dataArrayField = GameDataProviderUtils.GetDataArrayField(dataType);
                if (dataArrayField == null)
                {
                    return; // Error already logged by utility
                }

                var dataArray = dataArrayField.GetValue(null) as Array;
                if (dataArray == null)
                {
                    Debug.LogError($"Data array for {dataType.Name} is null");
                    return;
                }

                // Get the asset type
                var attr = dataType.GetCustomAttribute<GenerateAssetAttribute>();
                string assetTypeName = attr.AssetName ?? $"{dataType.Name}Asset";
                var assetType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.FullName == $"{GENERATED_NAMESPACE}.{assetTypeName}");

                // Update references for each asset
                foreach (var data in dataArray)
                {
                    if (assetPathCache.TryGetValue(data, out string assetPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                        if (asset != null)
                        {
                            UpdateAssetReferences(data, asset, dataType, assetType);
                            EditorUtility.SetDirty(asset);
                            Debug.Log($"Updated references for: {assetPath}");
                        }
                        else
                        {
                            Debug.LogError($"Could not load asset at path: {assetPath}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error updating references for type {dataType.Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private List<Type> SortTypesByDependencies(List<Type> types)
        {
            var result = new List<Type>();
            var visited = new HashSet<Type>();

            foreach (var type in types)
            {
                AddTypeToDependencyOrder(type, types, visited, result);
            }

            return result;
        }

        private void AddTypeToDependencyOrder(Type type, List<Type> allTypes, HashSet<Type> visited, List<Type> result)
        {
            if (visited.Contains(type))
                return;

            visited.Add(type);

            // Get all properties that reference other GenerateAsset types
            var dependencies = type.GetProperties()
                .SelectMany(p => GetReferencedTypes(p.PropertyType))
                .Where(t => allTypes.Contains(t))
                .Distinct();

            // Add dependencies first
            foreach (var dependency in dependencies)
            {
                AddTypeToDependencyOrder(dependency, allTypes, visited, result);
            }

            result.Add(type);
        }

        private IEnumerable<Type> GetReferencedTypes(Type type)
        {
            if (type.GetCustomAttribute<GenerateAssetAttribute>() != null)
            {
                yield return type;
            }

            if (type.IsGenericType)
            {
                foreach (var argType in type.GetGenericArguments())
                {
                    foreach (var referencedType in GetReferencedTypes(argType))
                    {
                        yield return referencedType;
                    }
                }
            }

            if (type.IsArray)
            {
                foreach (var referencedType in GetReferencedTypes(type.GetElementType()))
                {
                    yield return referencedType;
                }
            }
        }

        private void CreateAssetsForType(Type dataType)
        {
            try
            {
                Debug.Log($"Creating assets for type: {dataType.Name}");

                // Use the new utility class to find the data array
                var dataArrayField = GameDataProviderUtils.GetDataArrayField(dataType);
                if (dataArrayField == null)
                {
                    return; // Error already logged by utility
                }

                var dataArray = dataArrayField.GetValue(null) as Array;
                if (dataArray == null)
                {
                    Debug.LogError($"Data array for {dataType.Name} is null");
                    return;
                }

                // Get the asset type
                var attr = dataType.GetCustomAttribute<GenerateAssetAttribute>();
                string assetTypeName = attr.AssetName ?? $"{dataType.Name}Asset";
                var assetType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Type.EmptyTypes; }
                    })
                    .FirstOrDefault(t => t.FullName == $"{GENERATED_NAMESPACE}.{assetTypeName}");

                if (assetType == null)
                {
                    Debug.LogError($"Could not find generated asset type {GENERATED_NAMESPACE}.{assetTypeName}");
                    return;
                }

                // Create output directory
                string typePath = Path.Combine(assetOutputPath, $"{dataType.Name}s");
                if (!Directory.Exists(typePath))
                {
                    Directory.CreateDirectory(typePath);
                }

                // Create basic assets without references
                var fromDataMethod = assetType.GetMethod("FromData");
                foreach (var data in dataArray)
                {
                    var asset = fromDataMethod.Invoke(null, new[] { data });
                    string name = data.GetType().GetProperty("Name")?.GetValue(data) as string ?? "Unnamed";
                    name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                    string assetPath = Path.Combine(typePath, $"{name}.asset");

                    // Store in cache for later reference resolution
                    assetPathCache[data] = assetPath;

                    if (File.Exists(assetPath))
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }

                    AssetDatabase.CreateAsset(asset as ScriptableObject, assetPath);
                    Debug.Log($"Created asset: {assetPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating assets for type {dataType.Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private void UpdateAssetReferences(object sourceData, ScriptableObject targetAsset, Type dataType, Type assetType)
        {
            var properties = dataType.GetProperties();
            foreach (var prop in properties)
            {
                var propType = prop.PropertyType;
                if (HasAssetReference(propType))
                {
                    UpdatePropertyReference(sourceData, targetAsset, prop, dataType, assetType);
                }
            }

            EditorUtility.SetDirty(targetAsset);
        }

        private bool HasAssetReference(Type type)
        {
            if (type.GetCustomAttribute<GenerateAssetAttribute>() != null)
                return true;

            if (type.IsGenericType)
                return type.GetGenericArguments().Any(HasAssetReference);

            if (type.IsArray)
                return HasAssetReference(type.GetElementType());

            return false;
        }

        private void UpdatePropertyReference(object sourceData, ScriptableObject targetAsset, PropertyInfo prop, Type dataType, Type assetType)
        {
            var propType = prop.PropertyType;
            var value = prop.GetValue(sourceData);

            if (value == null)
                return;

            var targetProp = assetType.GetProperty(prop.Name);
            if (targetProp == null)
                return;

            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var sourceList = value as System.Collections.IList;
                var elementType = propType.GetGenericArguments()[0];

                if (elementType.GetCustomAttribute<GenerateAssetAttribute>() != null)
                {
                    var targetList = Activator.CreateInstance(targetProp.PropertyType) as System.Collections.IList;
                    foreach (var item in sourceList)
                    {
                        if (assetPathCache.TryGetValue(item, out string referencePath))
                        {
                            var referencedAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(referencePath);
                            targetList.Add(referencedAsset);
                        }
                    }
                    targetProp.SetValue(targetAsset, targetList);
                }
            }
            else if (propType.IsArray)
            {
                var sourceArray = value as Array;
                var elementType = propType.GetElementType();

                if (elementType.GetCustomAttribute<GenerateAssetAttribute>() != null)
                {
                    var targetArray = Array.CreateInstance(targetProp.PropertyType.GetElementType(), sourceArray.Length);
                    for (int i = 0; i < sourceArray.Length; i++)
                    {
                        if (assetPathCache.TryGetValue(sourceArray.GetValue(i), out string referencePath))
                        {
                            var referencedAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(referencePath);
                            targetArray.SetValue(referencedAsset, i);
                        }
                    }
                    targetProp.SetValue(targetAsset, targetArray);
                }
            }
            else if (propType.GetCustomAttribute<GenerateAssetAttribute>() != null)
            {
                if (assetPathCache.TryGetValue(value, out string referencePath))
                {
                    var referencedAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(referencePath);
                    targetProp.SetValue(targetAsset, referencedAsset);
                }
            }
        }

        private string GetArrayNameForType(Type dataType)
        {
            var arrayAttr = dataType.GetCustomAttribute<GameDataArrayAttribute>();
            if (arrayAttr != null)
            {
                return arrayAttr.ArrayName;
            }

            // Fallback to original behavior
            return $"{dataType.Name}s";
        }

        private void ForceRecompile()
        {
            try
            {
                // Force Unity to notice file changes
                AssetDatabase.Refresh();

                // Request script compilation
                CompilationPipeline.RequestScriptCompilation();

                Debug.Log("Force recompile initiated. Please wait for Unity to finish compiling...");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during force recompile: {e.Message}");
            }
        }
    }
}