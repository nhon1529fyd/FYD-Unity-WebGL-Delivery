# Cài đặt và cấu hình

## WordPress

1. Vào `Plugins > Add New > Upload Plugin` và chọn ZIP `fyd-unity-webgl-manager-v0.2.0.zip`.
2. Activate plugin. Plugin tạo database tables, cron cleanup và role `FYD Unity Deployer`.
3. Tạo user riêng với role `FYD Unity Deployer`; không dùng tài khoản admin chính.
4. Mở hồ sơ user đó, tạo Application Password và lưu lại chuỗi một lần.
5. Bảo đảm website dùng HTTPS.
6. Với Nginx, deny truy cập `/wp-content/uploads/fyd-unity/temp/`. Apache/LiteSpeed và IIS được plugin tạo deny file tự động.

## Unity

1. Mở project và chờ package compile.
2. Mở `Tools > FYD > Unity Publisher`.
3. Nhập App ID chữ thường, URL HTTPS, username và Application Password.
4. Bấm `Test Connection`.
5. Build từ Build Settings hoặc chọn thư mục WebGL build có sẵn.
6. Dùng `Build + Upload Chunks`.

Bản 0.2.0 dừng ở upload đầy đủ; chưa finalize thành staging release và không auto-activate.
