using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FYD.WebGLTools
{
    /// <summary>
    /// Optional project hook discovered from downstream Editor assemblies.
    /// A game can keep its own readiness logic without coupling the UPM package
    /// to game-specific namespaces or asset paths.
    /// </summary>
    public interface IFYDWebGLProjectExtension
    {
        bool PrepareForBuild(out string error);
        void AppendChecks(List<FYDCheckItem> items);
    }

    internal static class FYDWebGLProjectExtensions
    {
        private static readonly Lazy<IReadOnlyList<IFYDWebGLProjectExtension>>
            CachedExtensions =
                new Lazy<IReadOnlyList<IFYDWebGLProjectExtension>>(Discover);

        public static bool PrepareForBuild(out string error)
        {
            foreach (IFYDWebGLProjectExtension extension in CachedExtensions.Value)
            {
                try
                {
                    if (!extension.PrepareForBuild(out error))
                    {
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    error =
                        $"{extension.GetType().FullName} không thể chuẩn bị build: " +
                        exception.Message;
                    Debug.LogException(exception);
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static void AppendChecks(List<FYDCheckItem> items)
        {
            foreach (IFYDWebGLProjectExtension extension in CachedExtensions.Value)
            {
                try
                {
                    extension.AppendChecks(items);
                }
                catch (Exception exception)
                {
                    items.Add(new FYDCheckItem(
                        "Project WebGL extension",
                        FYDCheckStatus.Error,
                        $"{extension.GetType().FullName}: {exception.Message}"));
                    Debug.LogException(exception);
                }
            }
        }

        private static IReadOnlyList<IFYDWebGLProjectExtension> Discover()
        {
            return TypeCache.GetTypesDerivedFrom<IFYDWebGLProjectExtension>()
                .Where(type =>
                    !type.IsAbstract &&
                    !type.IsInterface &&
                    type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => (IFYDWebGLProjectExtension)Activator.CreateInstance(type))
                .OrderBy(extension => extension.GetType().FullName)
                .ToArray();
        }
    }
}
