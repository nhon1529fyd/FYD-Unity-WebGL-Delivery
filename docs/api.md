# FYD REST API v1

Base URL: `https://example.com/wp-json/fyd-unity/v1`

Mọi endpoint hiện tại yêu cầu WordPress Application Password qua Basic Auth.

## Status

`GET /status` — yêu cầu `upload_fyd_unity_builds`.

## Health

`GET /health` — yêu cầu `manage_fyd_unity_builds`.

## Applications

`GET /apps` — trả các app đã khởi tạo upload.

## Upload

- `POST /uploads/init`
- `PUT /uploads/{uploadId}/chunks/{index}`
- `GET /uploads/{uploadId}`
- `DELETE /uploads/{uploadId}`

Init nhận `appId`, `displayName`, `releaseId`, `releaseVersion`, `archiveSize`, `archiveSha256`, `chunkSize`, `totalChunks` và `manifest`.

Chunk request gửi raw binary cùng các header:

```text
X-FYD-App-ID
X-FYD-Upload-ID
X-FYD-Chunk-Index
X-FYD-Total-Chunks
X-FYD-Chunk-SHA256
```

Response thành công:

```json
{"ok":true,"data":{},"requestId":"fyd-..."}
```

Response nghiệp vụ thất bại:

```json
{"ok":false,"error":{"code":"...","message":"...","details":{}},"requestId":"fyd-..."}
```

Lỗi authentication/capability xảy ra trước callback có thể dùng error envelope chuẩn của WordPress REST API.
