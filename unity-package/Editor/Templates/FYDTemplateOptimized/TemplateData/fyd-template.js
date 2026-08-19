(function () {
  "use strict";

  var build = window.FYD_UNITY_BUILD || {};
  var params = new URLSearchParams(window.location.search);
  var app = document.getElementById("fyd-app");
  var stage = document.getElementById("game-stage");
  var canvas = document.getElementById("unity-canvas");
  var progressTrack = document.getElementById("unity-progress-track");
  var progressFill = document.getElementById("unity-progress-fill");
  var progressText = document.getElementById("loading-percent");
  var statusText = document.getElementById("loading-status");
  var retryButton = document.getElementById("retry-button");
  var warningRoot = document.getElementById("unity-warning");
  var transitionLayer = document.getElementById("transition-layer");
  var fullscreenButton = document.getElementById("fullscreen-button");

  var runtime = {
    instance: null,
    state: "idle",
    bootPromise: null,
    loaderScript: null,
    progress: 0,
    visuallyReady: false,
    waitForVisualReady: params.get("waitForVisualReady") === "1",
    parentOrigin: params.get("parentOrigin") || window.location.origin
  };

  function setState(nextState) {
    runtime.state = nextState;
    app.dataset.state = nextState;
    emit("STATE_CHANGED", { state: nextState });
  }

  function emit(type, payload) {
    var detail = {
      source: "FYD_UNITY_MODULE",
      type: type,
      moduleId: params.get("moduleId") || build.productName || "unity-module",
      productVersion: build.productVersion || "",
      payload: payload || {}
    };

    window.dispatchEvent(new CustomEvent("fydunityevent", { detail: detail }));

    if (window.parent !== window) {
      try {
        window.parent.postMessage(detail, runtime.parentOrigin);
      } catch (error) {
        console.warn("Không thể gửi sự kiện tới HTML Host:", error);
      }
    }
  }

  function setStatus(message) {
    statusText.textContent = message;
  }

  function setProgress(value) {
    var safeValue = Math.max(0, Math.min(1, Number(value) || 0));
    var percent = Math.round(safeValue * 100);
    runtime.progress = safeValue;
    progressFill.style.width = percent + "%";
    progressText.textContent = percent + "%";
    progressTrack.setAttribute("aria-valuenow", String(percent));
    emit("LOAD_PROGRESS", { progress: safeValue, percent: percent });
  }

  function showBanner(message, type) {
    var banner = document.createElement("div");
    banner.className = "unity-banner" + (type === "error" ? " error" : "");
    banner.textContent = String(message || "Đã xảy ra lỗi không xác định.");
    warningRoot.appendChild(banner);

    if (type !== "error") {
      window.setTimeout(function () {
        banner.remove();
      }, 5000);
    }
  }

  function assetUrl(filename) {
    if (!filename) return undefined;
    var version = encodeURIComponent(build.productVersion || "1");
    return build.buildUrl + "/" + filename + "?v=" + version;
  }

  function recommendedDpr() {
    var explicit = Number(params.get("dpr"));
    if (Number.isFinite(explicit) && explicit >= 0.75 && explicit <= 3) {
      return explicit;
    }

    var nativeDpr = window.devicePixelRatio || 1;
    var memory = navigator.deviceMemory || 4;
    var cores = navigator.hardwareConcurrency || 4;
    var quality = params.get("quality");
    var cap = 1.5;

    if (quality === "low") cap = 1;
    else if (quality === "high") cap = 2;
    else if (memory <= 3 || cores <= 4) cap = 1.25;
    else if (memory >= 8 && cores >= 8 && window.matchMedia("(min-width: 900px)").matches) cap = 2;

    return Math.max(1, Math.min(nativeDpr, cap));
  }

  function updateViewport() {
    var viewport = window.visualViewport;
    var width = Math.max(1, Math.round(viewport ? viewport.width : window.innerWidth));
    var height = Math.max(1, Math.round(viewport ? viewport.height : window.innerHeight));
    var aspect = 9 / 16;
    var stageWidth = Math.min(width, height * aspect);
    var stageHeight = stageWidth / aspect;

    if (stageHeight > height) {
      stageHeight = height;
      stageWidth = stageHeight * aspect;
    }

    document.documentElement.style.setProperty("--viewport-width", width + "px");
    document.documentElement.style.setProperty("--viewport-height", height + "px");
    stage.style.width = Math.round(stageWidth) + "px";
    stage.style.height = Math.round(stageHeight) + "px";
  }

  function loadLoaderScript() {
    if (window.createUnityInstance) return Promise.resolve();

    return new Promise(function (resolve, reject) {
      var script = document.createElement("script");
      var timeoutId = window.setTimeout(function () {
        script.remove();
        reject(new Error("Tải Unity Loader quá thời gian cho phép."));
      }, 60000);

      script.src = assetUrl(build.loaderFilename);
      script.async = true;
      script.onload = function () {
        window.clearTimeout(timeoutId);
        runtime.loaderScript = script;
        resolve();
      };
      script.onerror = function () {
        window.clearTimeout(timeoutId);
        script.remove();
        reject(new Error("Không tải được Unity Loader. Hãy kiểm tra đường dẫn Build và kết nối mạng."));
      };
      document.head.appendChild(script);
    });
  }

  function makeUnityConfig() {
    var config = {
      arguments: [],
      dataUrl: assetUrl(build.dataFilename),
      frameworkUrl: assetUrl(build.frameworkFilename),
      streamingAssetsUrl: build.streamingAssetsUrl || "StreamingAssets",
      companyName: build.companyName || "",
      productName: build.productName || "Unity Web Game",
      productVersion: build.productVersion || "1",
      showBanner: showBanner,
      devicePixelRatio: recommendedDpr(),
      autoSyncPersistentDataPath: build.autoSyncPersistentDataPath !== false
    };

    if (build.workerFilename) config.workerUrl = assetUrl(build.workerFilename);
    if (build.codeFilename) config.codeUrl = assetUrl(build.codeFilename);
    if (build.symbolsFilename) config.symbolsUrl = assetUrl(build.symbolsFilename);

    return config;
  }

  function revealWhenSafe() {
    if (runtime.waitForVisualReady && !runtime.visuallyReady) {
      setStatus("Đang hoàn thiện khung hình đầu tiên...");
      return;
    }

    window.requestAnimationFrame(function () {
      window.requestAnimationFrame(function () {
        window.setTimeout(function () {
          setState("ready");
          emit("MODULE_VISUALLY_READY", {});
          window.setTimeout(function () {
            if (runtime.state === "ready") setState("running");
          }, 360);
        }, 120);
      });
    });
  }

  function handleBootError(error) {
    console.error(error);
    setState("error");
    setStatus(error && error.message ? error.message : String(error));
    retryButton.hidden = false;
    showBanner(error && error.message ? error.message : String(error), "error");
    emit("BOOT_FAILED", { message: error && error.message ? error.message : String(error) });
  }

  function boot(entryData) {
    if (runtime.instance) return Promise.resolve(runtime.instance);
    if (runtime.bootPromise) return runtime.bootPromise;

    retryButton.hidden = true;
    runtime.visuallyReady = false;
    setProgress(0);
    setStatus("Đang tải bộ máy trò chơi...");
    setState("loading");

    runtime.bootPromise = loadLoaderScript()
      .then(function () {
        setStatus("Đang mở thế giới...");
        return window.createUnityInstance(canvas, makeUnityConfig(), setProgress);
      })
      .then(function (instance) {
        runtime.instance = instance;
        window.unityInstance = instance;
        setProgress(1);
        setStatus("Đã sẵn sàng.");
        emit("INSTANCE_READY", { dpr: recommendedDpr() });

        if (entryData != null) {
          sendMessage("WebBridge", "EnterModule", JSON.stringify(entryData));
        }

        revealWhenSafe();
        return instance;
      })
      .catch(function (error) {
        runtime.bootPromise = null;
        handleBootError(error);
        throw error;
      });

    return runtime.bootPromise;
  }

  function markVisualReady(payload) {
    runtime.visuallyReady = true;
    emit("UNITY_REPORTED_VISUAL_READY", payload || {});
    if (runtime.instance && (runtime.state === "loading" || runtime.state === "booting")) {
      revealWhenSafe();
    }
  }

  function sendMessage(objectName, methodName, value) {
    if (!runtime.instance || typeof runtime.instance.SendMessage !== "function") {
      return false;
    }

    if (typeof value === "undefined") runtime.instance.SendMessage(objectName, methodName);
    else runtime.instance.SendMessage(objectName, methodName, String(value));
    return true;
  }

  function showTransition(theme) {
    transitionLayer.dataset.theme = theme === "light" ? "light" : "dark";
    transitionLayer.classList.add("is-visible");
    emit("TRANSITION_SHOWN", { theme: transitionLayer.dataset.theme });
  }

  function hideTransition() {
    transitionLayer.classList.remove("is-visible");
    emit("TRANSITION_HIDDEN", {});
  }

  function shutdown(options) {
    var settings = options || {};
    if (!runtime.instance) {
      setState("stopped");
      return Promise.resolve();
    }

    setState("stopping");
    if (settings.transition !== false) showTransition(settings.theme || "dark");
    emit("SHUTDOWN_STARTED", {});

    return runtime.instance.Quit()
      .then(function () {
        runtime.instance = null;
        window.unityInstance = null;
        runtime.bootPromise = null;
        canvas.width = 1;
        canvas.height = 1;
        setState("stopped");
        emit("MODULE_DESTROYED", {});
      })
      .catch(function (error) {
        showBanner("Unity không thể đóng sạch: " + error, "error");
        emit("SHUTDOWN_FAILED", { message: String(error) });
        throw error;
      });
  }

  function retry() {
    warningRoot.replaceChildren();
    runtime.bootPromise = null;
    boot().catch(function () {});
  }

  function requestFullscreen() {
    var target = document.getElementById("fyd-app");
    var promise;

    if (document.fullscreenElement) promise = document.exitFullscreen();
    else if (target.requestFullscreen) promise = target.requestFullscreen();

    if (promise && typeof promise.catch === "function") {
      promise.catch(function (error) {
        showBanner("Không thể mở toàn màn hình: " + error, "warning");
      });
    }
  }

  function registerServiceWorker() {
    if (params.get("sw") === "0") return;
    if (!("serviceWorker" in navigator)) return;
    if (window.location.protocol !== "https:" && window.location.hostname !== "localhost") return;

    window.addEventListener("load", function () {
      navigator.serviceWorker.register("ServiceWorker.js", { scope: "./" }).catch(function (error) {
        console.warn("Service Worker không đăng ký được:", error);
      });
    });
  }

  function loadGoogleIdentity() {
    if (window.google && window.google.accounts) return Promise.resolve(window.google);
    if (window.__fydGoogleIdentityPromise) return window.__fydGoogleIdentityPromise;

    window.__fydGoogleIdentityPromise = new Promise(function (resolve, reject) {
      var script = document.createElement("script");
      script.src = "https://accounts.google.com/gsi/client";
      script.async = true;
      script.defer = true;
      script.onload = function () { resolve(window.google); };
      script.onerror = function () { reject(new Error("Không tải được Google Identity Services.")); };
      document.head.appendChild(script);
    });

    return window.__fydGoogleIdentityPromise;
  }

  window.unityGoogleLogin = window.unityGoogleLogin || {
    targetObject: "GoogleAuthBridge",
    callbackMethod: "OnGoogleSignIn",
    lastCredential: ""
  };

  window.handleCredentialResponse = function (response) {
    var credential = response && response.credential ? response.credential : "";
    window.unityGoogleLogin.lastCredential = credential;
    sendMessage(
      window.unityGoogleLogin.targetObject,
      window.unityGoogleLogin.callbackMethod,
      credential
    );
  };

  window.FYDGoogleAuth = {
    load: loadGoogleIdentity
  };

  window.FYDUnityModule = {
    boot: boot,
    shutdown: shutdown,
    sendMessage: sendMessage,
    markVisualReady: markVisualReady,
    showTransition: showTransition,
    hideTransition: hideTransition,
    getInstance: function () { return runtime.instance; },
    getState: function () { return runtime.state; },
    getProgress: function () { return runtime.progress; }
  };

  // Unity có thể gọi hàm này thông qua .jslib khi frame đầu tiên đã thật sự sẵn sàng.
  window.FYD_ModuleVisuallyReady = function (jsonPayload) {
    var payload = {};
    if (jsonPayload) {
      try { payload = JSON.parse(jsonPayload); }
      catch (_error) { payload = { value: String(jsonPayload) }; }
    }
    markVisualReady(payload);
  };

  window.addEventListener("message", function (event) {
    var allowed = runtime.parentOrigin === "*" || event.origin === runtime.parentOrigin;
    var message = event.data;
    if (!allowed || !message || message.source !== "FYD_HTML_HOST") return;

    switch (message.type) {
      case "BOOT_MODULE":
        boot(message.payload).catch(function () {});
        break;
      case "SHUTDOWN_MODULE":
        shutdown(message.payload).catch(function () {});
        break;
      case "SEND_TO_UNITY":
        if (message.payload) {
          sendMessage(message.payload.objectName, message.payload.methodName, message.payload.value);
        }
        break;
      case "SHOW_TRANSITION":
        showTransition(message.payload && message.payload.theme);
        break;
      case "HIDE_TRANSITION":
        hideTransition();
        break;
      default:
        break;
    }
  });

  window.addEventListener("resize", updateViewport, { passive: true });
  if (window.visualViewport) {
    window.visualViewport.addEventListener("resize", updateViewport, { passive: true });
  }
  window.addEventListener("orientationchange", updateViewport, { passive: true });
  document.addEventListener("visibilitychange", function () {
    emit("VISIBILITY_CHANGED", { hidden: document.hidden });
  });

  retryButton.addEventListener("click", retry);
  fullscreenButton.addEventListener("click", requestFullscreen);

  updateViewport();
  registerServiceWorker();
  emit("TEMPLATE_READY", { autoBoot: params.get("autoboot") !== "0" });

  if (params.get("autoboot") !== "0") {
    boot().catch(function () {});
  }
})();
