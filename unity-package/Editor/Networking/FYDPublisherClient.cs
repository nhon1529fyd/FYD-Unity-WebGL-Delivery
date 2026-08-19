using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FYD.UnityPublisher.Editor.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace FYD.UnityPublisher.Editor.Networking
{
    /// <summary>Authenticated REST client for the FYD WordPress plugin.</summary>
    public sealed class FYDPublisherClient
    {
        private readonly string _apiRoot;
        private readonly string _username;
        private readonly string _password;
        private readonly int _timeoutSeconds;

        public FYDPublisherClient(string websiteUrl, string username, string applicationPassword, int timeoutSeconds)
        {
            if (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out Uri uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Website URL phải là địa chỉ HTTPS hợp lệ.", nameof(websiteUrl));
            }
            _apiRoot = websiteUrl.TrimEnd('/') + "/wp-json/fyd-unity/v1";
            _username = username ?? string.Empty;
            _password = applicationPassword ?? string.Empty;
            _timeoutSeconds = Math.Max(15, timeoutSeconds);
        }

        public async Task<FYDStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
        {
            string json = await SendAsync("GET", "/status", null, null, cancellationToken);
            FYDStatusResponse response = JsonUtility.FromJson<FYDStatusResponse>(json);
            EnsureSuccess(response != null && response.ok, response?.error, response?.requestId, 200);
            return response;
        }

        public async Task<FYDUploadInitResponse> InitializeUploadAsync(
            FYDUploadInitRequest payload,
            CancellationToken cancellationToken)
        {
            string json = await SendAsync("POST", "/uploads/init", Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)),
                new Dictionary<string, string> { { "Content-Type", "application/json; charset=utf-8" } }, cancellationToken);
            FYDUploadInitResponse response = JsonUtility.FromJson<FYDUploadInitResponse>(json);
            EnsureSuccess(response != null && response.ok, response?.error, response?.requestId, 200);
            return response;
        }

        public async Task<FYDUploadStatusResponse> GetUploadStatusAsync(string uploadId, CancellationToken cancellationToken)
        {
            string json = await SendAsync("GET", "/uploads/" + Uri.EscapeDataString(uploadId), null, null, cancellationToken);
            FYDUploadStatusResponse response = JsonUtility.FromJson<FYDUploadStatusResponse>(json);
            EnsureSuccess(response != null && response.ok, response?.error, response?.requestId, 200);
            return response;
        }

        public Task UploadChunkAsync(
            string appId,
            string uploadId,
            int index,
            int totalChunks,
            byte[] bytes,
            string sha256,
            CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/octet-stream" },
                { "X-FYD-App-ID", appId },
                { "X-FYD-Upload-ID", uploadId },
                { "X-FYD-Chunk-Index", index.ToString() },
                { "X-FYD-Total-Chunks", totalChunks.ToString() },
                { "X-FYD-Chunk-SHA256", sha256 }
            };
            return SendAsync("PUT", "/uploads/" + Uri.EscapeDataString(uploadId) + "/chunks/" + index, bytes, headers, cancellationToken);
        }

        private async Task<string> SendAsync(
            string method,
            string route,
            byte[] body,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            using (var request = new UnityWebRequest(_apiRoot + route, method))
            {
                if (body != null) request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = _timeoutSeconds;
                string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(_username + ":" + _password));
                request.SetRequestHeader("Authorization", "Basic " + basic);
                request.SetRequestHeader("Accept", "application/json");
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers) request.SetRequestHeader(header.Key, header.Value);
                }

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                string responseText = request.downloadHandler?.text ?? string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    FYDStatusResponse errorResponse = null;
                    try { errorResponse = JsonUtility.FromJson<FYDStatusResponse>(responseText); } catch { }
                    string message = errorResponse?.error?.message;
                    if (string.IsNullOrWhiteSpace(message)) message = "REST request thất bại (HTTP " + request.responseCode + ").";
                    throw new FYDPublisherApiException(
                        errorResponse?.error?.code ?? "http_error",
                        message,
                        errorResponse?.requestId,
                        request.responseCode);
                }
                return responseText;
            }
        }

        private static void EnsureSuccess(bool ok, FYDApiError error, string requestId, long statusCode)
        {
            if (ok) return;
            throw new FYDPublisherApiException(
                error?.code ?? "invalid_response",
                error?.message ?? "Server trả response không hợp lệ.",
                requestId,
                statusCode);
        }
    }
}
