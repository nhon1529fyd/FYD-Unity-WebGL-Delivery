# FYD Unity WebGL Manager 0.2.1

## Yêu cầu

- WordPress 6.x
- PHP 8.0+
- HTTPS
- Application Passwords bật
- PHP ZIP extension cần cho giai đoạn finalize

## Quyền

- `upload_fyd_unity_builds`: status, apps, init/chunk/status/cancel upload.
- `manage_fyd_unity_builds`: health và các thao tác quản trị release trong giai đoạn sau.

Plugin tạo role `FYD Unity Deployer` chỉ có quyền đọc và upload; không có quyền activate/rollback/delete.

Upload tạm hết hạn sau 24 giờ, nằm dưới WordPress uploads và được chặn truy cập trực tiếp bằng Apache/LiteSpeed `.htaccess` cùng IIS `web.config`. Với Nginx, quản trị viên cần cấu hình deny cho `/wp-content/uploads/fyd-unity/temp/` trước khi dùng production.
