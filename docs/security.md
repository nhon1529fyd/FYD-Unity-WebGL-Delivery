# Security notes

- Unity Publisher chỉ chấp nhận URL HTTPS.
- Application Password không được ghi vào Assets, manifest, ZIP hoặc log.
- Endpoint ghi dữ liệu dùng capability `upload_fyd_unity_builds`; không route ghi nào dùng permission callback công khai.
- Session gắn với user tạo upload. User khác chỉ truy cập được khi có capability quản trị.
- Chunk bị giới hạn 1-20 MiB, kiểm tra đúng kích thước và SHA-256 trước khi ghi.
- Ghi chunk và metadata dùng file tạm trong cùng thư mục rồi rename.
- Build có PHP/executable bị từ chối. `.htaccess`, `.user.ini` và `web.config` bị loại khỏi ZIP Unity.
- Max archive mặc định 500 MiB; upload hết hạn sau 24 giờ.
- Authorization header, password và raw chunk không được ghi vào deployment log.

ZIP Slip, zip bomb và quét sau giải nén được triển khai trong giai đoạn Validation/Release trước khi bật finalize.
