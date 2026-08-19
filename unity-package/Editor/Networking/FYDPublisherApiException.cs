using System;

namespace FYD.UnityPublisher.Editor.Networking
{
    public sealed class FYDPublisherApiException : Exception
    {
        public FYDPublisherApiException(string code, string message, string requestId, long statusCode)
            : base(message)
        {
            Code = code ?? "request_failed";
            RequestId = requestId ?? string.Empty;
            StatusCode = statusCode;
        }

        public string Code { get; }
        public string RequestId { get; }
        public long StatusCode { get; }
    }
}
