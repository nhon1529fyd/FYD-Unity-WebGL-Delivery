# FYD Unity WebGL Delivery System

Hệ thống phát hành Unity WebGL qua WordPress theo phiên bản, dùng HTTPS, SHA-256
và upload theo chunk.

## Trạng thái hiện tại

- Unity UPM package `com.fyd.unity-publisher` v0.3.0 đã gom chung:
  - WebGL setup, checklist và builder;
  - `FYDTemplateOptimized`;
  - runtime bridge Unity → HTML host;
  - publisher, deterministic ZIP và chunk uploader.
- Cấu hình riêng từng game nằm trong `ProjectSettings`, credential nằm trong
  `EditorPrefs`.
- Logic riêng của Imperial Bloodline nằm lại trong project qua
  `IFYDWebGLProjectExtension`.
- WordPress plugin v0.2.1 đã cài và kích hoạt trên `english.fydhub.com`.

Finalize, giải nén staging, activate, rollback, shortcode và giao diện quản trị vẫn là
các giai đoạn tiếp theo của delivery system.

## Cấu trúc

```text
FYD-Unity-WebGL-Delivery/
├── unity-package/
├── wordpress-plugin/fyd-unity-webgl-manager/
├── docs/
└── dist/
```

## Cài UPM package

Có thể dùng package bằng local dependency:

```json
"com.fyd.unity-publisher": "file:../FYD-Unity-WebGL-Delivery/unity-package"
```

Project khác có thể cài trực tiếp từ GitHub bằng:

```text
https://github.com/nhon1529fyd/FYD-Unity-WebGL-Delivery.git?path=/unity-package#v0.3.0
```

Trong Unity:

- mở `FYD > WebGL > Setup & Builder` để cấu hình/checklist/build;
- mở `Tools > FYD > Unity Publisher` để test kết nối, package và upload.

Xem thêm [deployment](docs/deployment.md), [API](docs/api.md),
[security](docs/security.md) và [troubleshooting](docs/troubleshooting.md).

## Kiểm thử

Unity EditMode tests nằm trong assembly `FYD.UnityPublisher.Editor.Tests`. Bản v0.3.0
đã chạy thành công 11/11 tests trên Unity 6000.3.20f1.

WordPress static security test:

```powershell
php FYD-Unity-WebGL-Delivery/wordpress-plugin/fyd-unity-webgl-manager/tests/security-static.php
```
