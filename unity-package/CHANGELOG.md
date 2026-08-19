# Changelog

## 0.3.0 - 2026-07-30

- Gom WebGL setup/builder, publisher, runtime bridge và nguồn template vào một UPM
  package duy nhất.
- Chuyển cấu hình FYD WebGL theo từng game sang `ProjectSettings`.
- Tự di chuyển cấu hình cũ từ `Assets/FYDWebGLTools/FYDWebGLSettings.asset`.
- Thêm trình cài/đồng bộ `FYDTemplateOptimized` từ package.
- Thêm `IFYDWebGLProjectExtension` cho các kiểm tra và bước chuẩn bị riêng của game.
- Giữ nguyên package ID `com.fyd.unity-publisher` để không làm hỏng project 0.2.x.

## 0.2.0 - 2026-07-29

- Thêm WebGL build wrapper và output validation.
- Thêm deterministic ZIP, manifest schema 1 và SHA-256.
- Thêm HTTPS REST client, chunk upload, resume và retry.
- Thêm Editor window và local credential handling.

## 0.1.0 - 2026-07-29

- Tạo package metadata, Editor assembly và project settings.
