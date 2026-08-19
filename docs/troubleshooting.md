# Troubleshooting

## Test Connection trả 401/403

- Kiểm tra đúng username và Application Password, không dùng password đăng nhập WordPress.
- Kiểm tra user có role `FYD Unity Deployer` hoặc capability `upload_fyd_unity_builds`.
- Kiểm tra proxy/hosting không loại bỏ header `Authorization`.

## Unity từ chối URL

Publisher chỉ chấp nhận HTTPS theo đặc tả. Cài chứng chỉ hợp lệ cho website trước khi upload.

## Build bị từ chối vì `.php` hoặc executable

Xóa file không thuộc Unity WebGL output. File server config `.htaccess`, `.user.ini`, `web.config` được Publisher tự loại khỏi ZIP.

## Upload bị ngắt

Bấm Upload lại trong cùng phiên Editor. Publisher hỏi status và chỉ gửi các chunk còn thiếu. Session mặc định hết hạn sau 24 giờ.

## Request quá lớn

Giảm chunk size trong Unity xuống 1-2 MiB. Chunk upload dùng raw body nhưng reverse proxy vẫn có thể đặt giới hạn riêng.
