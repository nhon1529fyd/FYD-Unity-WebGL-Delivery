using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FYD.WebGLTools
{
    internal static class FYDWebGLCompatibility
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static bool TryGetWebGLProperty<T>(string propertyName, out T value)
        {
            value = default;
            PropertyInfo property = typeof(PlayerSettings.WebGL).GetProperty(propertyName, StaticFlags);
            if (property == null || !property.CanRead)
            {
                return false;
            }

            try
            {
                object raw = property.GetValue(null, null);
                if (raw is T typed)
                {
                    value = typed;
                    return true;
                }

                if (raw != null)
                {
                    value = (T)Convert.ChangeType(raw, typeof(T));
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"FYD WebGL Tools: Không đọc được PlayerSettings.WebGL.{propertyName}: {exception.Message}");
            }

            return false;
        }

        public static bool TrySetWebGLProperty(string propertyName, object value)
        {
            PropertyInfo property = typeof(PlayerSettings.WebGL).GetProperty(propertyName, StaticFlags);
            if (property == null || !property.CanWrite)
            {
                return false;
            }

            try
            {
                object converted = value;
                Type targetType = property.PropertyType;

                if (targetType.IsEnum && value is string enumName)
                {
                    converted = Enum.Parse(targetType, enumName, true);
                }
                else if (value != null && !targetType.IsInstanceOfType(value))
                {
                    converted = Convert.ChangeType(value, targetType);
                }

                property.SetValue(null, converted, null);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"FYD WebGL Tools: Không đặt được PlayerSettings.WebGL.{propertyName}: {exception.Message}");
                return false;
            }
        }

        public static bool TryGetWebGLEnumName(string propertyName, out string enumName)
        {
            enumName = string.Empty;
            PropertyInfo property = typeof(PlayerSettings.WebGL).GetProperty(propertyName, StaticFlags);
            if (property == null || !property.CanRead)
            {
                return false;
            }

            try
            {
                object value = property.GetValue(null, null);
                enumName = value != null ? value.ToString() : string.Empty;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SetRecommendedMemoryGrowthIfSupported()
        {
            // Available in modern Unity versions. Reflection keeps the tool compatible
            // with older LTS releases where these properties do not exist.
            TrySetWebGLProperty("memoryGrowthMode", "Geometric");
        }

        public static bool IsMemoryGrowthGeometricOrUnavailable()
        {
            return !TryGetWebGLEnumName("memoryGrowthMode", out string mode) ||
                   string.Equals(mode, "Geometric", StringComparison.OrdinalIgnoreCase);
        }
    }
}
