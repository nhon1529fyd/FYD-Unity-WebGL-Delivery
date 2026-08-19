using System.Runtime.InteropServices;
using UnityEngine;

namespace FYD.WebGLTools
{
    /// <summary>
    /// Minimal bridge from Unity to FYDTemplateOptimized/HTML Host.
    /// Call ReportVisualReady only after the scene, camera and visible content are ready.
    /// </summary>
    public static class FYDWebGLModuleBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FYD_ReportVisualReady(string payloadJson);

        [DllImport("__Internal")]
        private static extern void FYD_EmitHostEvent(string eventType, string payloadJson);
#endif

        public static void ReportVisualReady(string payloadJson = "{}")
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FYD_ReportVisualReady(string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson);
#else
            Debug.Log("FYD WebGL Visual Ready: " + payloadJson);
#endif
        }

        public static void EmitHostEvent(string eventType, string payloadJson = "{}")
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                Debug.LogWarning("FYDWebGLModuleBridge.EmitHostEvent cần eventType.");
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            FYD_EmitHostEvent(eventType, string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson);
#else
            Debug.Log($"FYD WebGL Event: {eventType} {payloadJson}");
#endif
        }
    }

    /// <summary>
    /// Optional helper for simple scenes. For complex modules, call
    /// FYDWebGLModuleBridge.ReportVisualReady manually at the correct moment.
    /// </summary>
    public sealed class FYDWebGLVisualReadyReporter : MonoBehaviour
    {
        [Min(0)] [SerializeField] private int waitFrames = 2;
        [SerializeField] private string payloadJson = "{}";

        private System.Collections.IEnumerator Start()
        {
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            FYDWebGLModuleBridge.ReportVisualReady(payloadJson);
        }
    }
}
