# FYD WebGL Tools

Một UPM package thống nhất cho quy trình Unity WebGL của FYD:

- cấu hình WebGL và checklist trước build;
- build production/development và tạo thư mục deploy ổn định;
- runtime bridge `ReportVisualReady` / `EmitHostEvent`;
- nguồn `FYDTemplateOptimized` và trình cài template vào project;
- đóng gói, kiểm tra và upload theo chunk đến FYD Unity WebGL Manager trên WordPress.

## Cài bằng local path

Thêm dependency sau vào `Packages/manifest.json`:

```json
"com.fyd.unity-publisher": "file:../FYD-Unity-WebGL-Delivery/unity-package"
```

Package giữ nguyên ID `com.fyd.unity-publisher` để tương thích với các project đã cài
từ phiên bản 0.2.x.

## Cài từ Git

Unity Package Manager có thể cài trực tiếp package này bằng URL:

```text
https://github.com/nhon1529fyd/FYD-Unity-WebGL-Delivery.git?path=/unity-package#v0.3.0
```

Mở `Window > Package Manager > + > Add package from git URL` rồi dán URL trên.

## Sử dụng

- `FYD > WebGL > Setup & Builder`: cấu hình project và chạy checklist.
- `FYD > WebGL > Build Production`: tạo release WebGL.
- `FYD > WebGL > Install or Refresh Template`: đồng bộ template từ package.
- `Tools > FYD > Unity Publisher`: test kết nối, đóng gói và upload WordPress.

Cấu hình build WebGL nằm tại
`ProjectSettings/FYDWebGLToolsSettings.asset`. Cấu hình publisher nằm tại
`ProjectSettings/FYDUnityPublisherSettings.asset`.

Application Password không nằm trong Assets, manifest, ZIP hoặc Console. Khi bật lưu
cục bộ, credential chỉ được lưu bằng `EditorPrefs`; nút `Forget Credential` xóa giá trị
này.

Template gốc thuộc package. Unity chỉ nhận custom WebGL template từ
`Assets/WebGLTemplates`, vì vậy package tự tạo một working copy tại
`Assets/WebGLTemplates/FYDTemplateOptimized`.

## Mở rộng theo từng game

Logic riêng của game không nên đưa vào package chung. Tạo một lớp Editor triển khai
`IFYDWebGLProjectExtension`; package sẽ tự phát hiện lớp đó và ghép các bước chuẩn bị,
checklist riêng vào quy trình build.

Xem thêm tại `Documentation~/ProjectExtensions.md`.
