mergeInto(LibraryManager.library, {
  FYD_ReportVisualReady: function (payloadPtr) {
    var raw = payloadPtr ? UTF8ToString(payloadPtr) : "{}";
    var payload = {};

    try {
      payload = JSON.parse(raw || "{}");
    } catch (error) {
      payload = { raw: raw, parseError: String(error) };
    }

    if (window.FYDUnityModule && typeof window.FYDUnityModule.markVisualReady === "function") {
      window.FYDUnityModule.markVisualReady(payload);
      return;
    }

    if (typeof window.FYD_ModuleVisuallyReady === "function") {
      window.FYD_ModuleVisuallyReady(JSON.stringify(payload));
    }
  },

  FYD_EmitHostEvent: function (eventTypePtr, payloadPtr) {
    var eventType = eventTypePtr ? UTF8ToString(eventTypePtr) : "UNITY_EVENT";
    var raw = payloadPtr ? UTF8ToString(payloadPtr) : "{}";
    var payload = {};

    try {
      payload = JSON.parse(raw || "{}");
    } catch (error) {
      payload = { raw: raw, parseError: String(error) };
    }

    var detail = {
      source: "FYD_UNITY_RUNTIME",
      type: eventType,
      payload: payload
    };

    window.dispatchEvent(new CustomEvent("fydunityevent", { detail: detail }));

    if (window.parent !== window) {
      try {
        window.parent.postMessage(detail, window.location.origin);
      } catch (error) {
        console.warn("FYD bridge postMessage failed:", error);
      }
    }
  }
});
