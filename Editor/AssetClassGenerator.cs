using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using Common.Data.Attributes;

namespace Game.Editor.CodeGen
{
    public class AssetClassGenerator
    {
        private const string GENERATED_FOLDER = "Assets/Scripts/Generated";
        private const string NAMESPACE = "Game.Assets.Generated";
        private static Dictionary<Type, Type> generatedTypeMap = new Dictionary<Type, Type>();

        public static void GenerateAssetClasses()
        {
            // Ensure the output directory exists
            if (!Directory.Exists(GENERATED_FOLDER))
            {
                Directory.CreateDirectory(GENERATED_FOLDER);
            }

            // Find the assembly containing our Common types
            var commonAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetTypes()
                    .Any(t => t.Namespace?.StartsWith("Common.Data") == true));

            if (commonAssembly == null)
            {
                Debug.LogError("Could not find assembly containing Common.Data namespace.");
                return;
            }

            // Get all types with GenerateAsset attribute
            var typesToGenerate = commonAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<GenerateAssetAttribute>() != null)
                .ToList();

            if (!typesToGenerate.Any())
            {
                Debug.LogWarning("No types found with GenerateAsset attribute.");
                return;
            }

            // First pass: Register all types that will be generated
            foreach (var type in typesToGenerate)
            {
                var attr = type.GetCustomAttribute<GenerateAssetAttribute>();
                string assetClassName = attr.AssetName ?? $"{type.Name}Asset";
                Type assetType = Type.GetType($"{NAMESPACE}.{assetClassName}, Assembly-CSharp");
                generatedTypeMap[type] = assetType;
            }

            // Second pass: Generate asset classes
            foreach (var sourceType in typesToGenerate)
            {
                try
                {
                    GenerateAssetClass(sourceType);
                    Debug.Log($"Generated asset class for {sourceType.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error generating asset class for {sourceType.Name}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
        }

        private static void GenerateAssetClass(Type sourceType)
        {
            var attr = sourceType.GetCustomAttribute<GenerateAssetAttribute>();
            string assetClassName = attr.AssetName ?? $"{sourceType.Name}Asset";

            var builder = new StringBuilder();

            // Generate using statements
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine($"using {sourceType.Namespace};");
            builder.AppendLine();

            // Generate namespace
            builder.AppendLine($"namespace {NAMESPACE}");
            builder.AppendLine("{");

            // Generate class declaration - maintain abstract if source is abstract
            var abstractModifier = sourceType.IsAbstract ? " abstract" : "";
            builder.AppendLine($"    public{abstractModifier} class {assetClassName} : ScriptableObject");
            builder.AppendLine("    {");

            // Generate fields and properties
            var properties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => !prop.GetCustomAttributes<IgnoreGenerationAttribute>().Any())
                .ToArray();

            foreach (var prop in properties)
            {
                var propertyType = prop.PropertyType;
                string unityType = GetUnityCompatibleType(propertyType);

                builder.AppendLine($"        [SerializeField]");
                builder.AppendLine($"        private {unityType} _{prop.Name.ToLower()};");
                builder.AppendLine();

                builder.AppendLine($"        public {unityType} {prop.Name}");
                builder.AppendLine("        {");
                builder.AppendLine($"            get => _{prop.Name.ToLower()};");
                builder.AppendLine($"            set => _{prop.Name.ToLower()} = value;");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            // Only generate FromData and ToData methods for non-abstract classes
            if (!sourceType.IsAbstract)
            {
                // Generate FromData method
                builder.AppendLine($"        public static {assetClassName} FromData({sourceType.Name} data)");
                builder.AppendLine("        {");
                builder.AppendLine($"            var asset = CreateInstance<{assetClassName}>();");
                foreach (var prop in properties)
                {
                    var convertedAssignment = GeneratePropertyAssignment(prop, "data", "asset", true);
                    builder.AppendLine($"            {convertedAssignment}");
                }
                builder.AppendLine("            return asset;");
                builder.AppendLine("        }");
                builder.AppendLine();

                // Generate ToData method
                builder.AppendLine($"        public {sourceType.Name} ToData()");
                builder.AppendLine("        {");
                builder.AppendLine($"            return new {sourceType.Name}");
                builder.AppendLine("            {");

                for (int i = 0; i < properties.Length; i++)
                {
                    var prop = properties[i];
                    var convertedAssignment = GeneratePropertyAssignment(prop, "this", "result", false);
                    builder.AppendLine($"                {prop.Name} = {convertedAssignment}{(i < properties.Length - 1 ? "," : "")}");
                }

                builder.AppendLine("            };");
                builder.AppendLine("        }");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            // Write the generated code to a file
            string filePath = Path.Combine(GENERATED_FOLDER, $"{assetClassName}.cs");
            File.WriteAllText(filePath, builder.ToString());
        }

        private static string GetUnityCompatibleType(Type type)
        {
            // Handle collections
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var unityElementType = GetUnityCompatibleType(elementType);
                return $"List<{unityElementType}>";
            }

            // Handle arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var unityElementType = GetUnityCompatibleType(elementType);
                return $"{unityElementType}[]";
            }

            // Handle nested types with GenerateAsset attribute
            if (type.GetCustomAttribute<GenerateAssetAttribute>() != null)
            {
                var attr = type.GetCustomAttribute<GenerateAssetAttribute>();
                return attr.AssetName ?? $"{type.Name}Asset";
            }

            // Handle basic type mappings
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Decimal:
                    return "double";
                default:
                    return type.Name;
            }
        }

        private static string GeneratePropertyAssignment(PropertyInfo prop, string sourceObj, string targetObj, bool toAsset)
        {
            if (prop.GetCustomAttributes<IgnoreGenerationAttribute>().Any())
            {
                return null; // Skip this property
            }


            var propertyType = prop.PropertyType;
            var propName = prop.Name;

            // Handle collections
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = propertyType.GetGenericArguments()[0];
                if (elementType.GetCustomAttribute<GenerateAssetAttribute>() != null)
                {
                    if (toAsset)
                        return $"{targetObj}.{propName} = {sourceObj}.{propName}?.ConvertAll(x => {GetUnityCompatibleType(elementType)}.FromData(x));";
                    else
                        return $"{sourceObj}.{propName}?.ConvertAll(x => x.ToData())";
                }
            }

            // Handle arrays
            if (propertyType.IsArray)
            {
                var elementType = propertyType.GetElementType();
                if (elementType.GetCustomAttribute<GenerateAssetAttribute>() != null)
                {
                    if (toAsset)
                        return $"{targetObj}.{propName} = {sourceObj}.{propName}?.Select(x => {GetUnityCompatibleType(elementType)}.FromData(x)).ToArray();";
                    else
                        return $"{sourceObj}.{propName}?.Select(x => x.ToData()).ToArray()";
                }
            }

            // Handle nested types with GenerateAsset attribute
            if (propertyType.GetCustomAttribute<GenerateAssetAttribute>() != null)
            {
                if (toAsset)
                    return $"{targetObj}.{propName} = {sourceObj}.{propName} != null ? {GetUnityCompatibleType(propertyType)}.FromData({sourceObj}.{propName}) : null;";
                else
                    return $"{sourceObj}.{propName}?.ToData()";
            }

            // Handle basic types
            if (toAsset)
                return $"{targetObj}.{propName} = {sourceObj}.{propName};";
            else
                return $"{sourceObj}.{propName}";
        }
    }
}