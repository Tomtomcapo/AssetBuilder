using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Common.Data.Attributes;

namespace AssetBuilder.Editor
{
    internal static class GameDataProviderUtils
    {
        private static Dictionary<Type, FieldInfo> cachedArrayFields = new Dictionary<Type, FieldInfo>();
        private static HashSet<Type> scannedProviders = new HashSet<Type>();
        private static bool hasScannedAssemblies = false;

        public static void ClearCache()
        {
            cachedArrayFields.Clear();
            scannedProviders.Clear();
            hasScannedAssemblies = false;
        }

        public static FieldInfo GetDataArrayField(Type dataType)
        {
            // Return cached result if available
            if (cachedArrayFields.TryGetValue(dataType, out var cachedField))
            {
                return cachedField;
            }

            // Scan assemblies if we haven't yet
            if (!hasScannedAssemblies)
            {
                ScanAssembliesForProviders();
            }

            // Get array name from GameDataArray attribute
            string arrayName = GetArrayNameForType(dataType);
            if (string.IsNullOrEmpty(arrayName))
            {
                Debug.LogError($"Type {dataType.Name} is missing GameDataArray attribute");
                return null;
            }

            // Look for the array in all provider classes
            foreach (var providerType in scannedProviders)
            {
                var field = providerType.GetField(arrayName, BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    // Cache and return the result
                    cachedArrayFields[dataType] = field;
                    return field;
                }
            }

            Debug.LogError($"Could not find array '{arrayName}' in any GameDataProvider class");
            return null;
        }

        private static void ScanAssembliesForProviders()
        {
            try
            {
                // Get all loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Skip Unity assemblies and system assemblies
                        if (assembly.FullName.StartsWith("Unity") || 
                            assembly.FullName.StartsWith("System") || 
                            assembly.FullName.StartsWith("Microsoft"))
                        {
                            continue;
                        }

                        // Find all types with GameDataProvider attribute
                        var providerTypes = assembly.GetTypes()
                            .Where(t => t.GetCustomAttribute<GameDataProviderAttribute>() != null);

                        foreach (var providerType in providerTypes)
                        {
                            if (!providerType.IsAbstract || !providerType.IsSealed)
                            {
                                Debug.LogWarning($"GameDataProvider class {providerType.Name} must be static (abstract and sealed)");
                                continue;
                            }

                            scannedProviders.Add(providerType);
                            Debug.Log($"Found GameDataProvider: {providerType.FullName}");
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        // Skip assemblies that can't be loaded
                        continue;
                    }
                }

                hasScannedAssemblies = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error scanning assemblies for GameDataProviders: {e.Message}");
            }
        }

        private static string GetArrayNameForType(Type dataType)
        {
            var arrayAttr = dataType.GetCustomAttribute<GameDataArrayAttribute>();
            return arrayAttr?.ArrayName;
        }
    }
}