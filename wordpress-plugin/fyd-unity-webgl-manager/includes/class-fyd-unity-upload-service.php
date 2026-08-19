<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Implements upload init, chunk persistence, status/resume and cancellation. */
final class FYD_Unity_Upload_Service {
	private const MIN_CHUNK_SIZE  = 1048576;
	private const MAX_CHUNK_SIZE  = 20971520;
	private const DEFAULT_TTL     = 86400;
	private const DEFAULT_MAX_ZIP = 524288000;

	private FYD_Unity_Storage $storage;
	private FYD_Unity_Logger $logger;

	public function __construct( FYD_Unity_Storage $storage, FYD_Unity_Logger $logger ) {
		$this->storage = $storage;
		$this->logger  = $logger;
	}

	public function initialize( WP_REST_Request $request, string $request_id ): WP_REST_Response {
		$params = $request->get_json_params();
		if ( ! is_array( $params ) ) {
			return FYD_Unity_Response::error( 'invalid_json', 'Request JSON không hợp lệ.', $request_id, 400 );
		}

		$app_id          = isset( $params['appId'] ) ? strtolower( trim( (string) $params['appId'] ) ) : '';
		$release_id      = isset( $params['releaseId'] ) ? trim( (string) $params['releaseId'] ) : '';
		$release_version = isset( $params['releaseVersion'] ) ? sanitize_text_field( (string) $params['releaseVersion'] ) : '';
		$display_name    = isset( $params['displayName'] ) ? sanitize_text_field( (string) $params['displayName'] ) : $app_id;
		$archive_size    = isset( $params['archiveSize'] ) ? (int) $params['archiveSize'] : 0;
		$archive_hash    = isset( $params['archiveSha256'] ) ? strtolower( (string) $params['archiveSha256'] ) : '';
		$chunk_size      = isset( $params['chunkSize'] ) ? (int) $params['chunkSize'] : 5 * self::MIN_CHUNK_SIZE;
		$total_chunks    = isset( $params['totalChunks'] ) ? (int) $params['totalChunks'] : 0;

		if ( ! preg_match( '/^[a-z0-9]+(?:-[a-z0-9]+)*$/', $app_id ) || strlen( $app_id ) > 100 ) {
			return FYD_Unity_Response::error( 'invalid_app_id', 'App ID không hợp lệ.', $request_id, 422 );
		}
		if ( ! preg_match( '/^[a-z0-9][a-z0-9.-]{0,189}$/', $release_id ) ) {
			return FYD_Unity_Response::error( 'invalid_release_id', 'Release ID không hợp lệ.', $request_id, 422 );
		}
		if ( ! preg_match( '/^[a-f0-9]{64}$/', $archive_hash ) ) {
			return FYD_Unity_Response::error( 'invalid_archive_hash', 'SHA-256 archive không hợp lệ.', $request_id, 422 );
		}
		$max_archive = max( self::MIN_CHUNK_SIZE, (int) get_option( 'fyd_unity_max_archive_size', self::DEFAULT_MAX_ZIP ) );
		if ( $archive_size <= 0 || $archive_size > $max_archive ) {
			return FYD_Unity_Response::error( 'archive_size_rejected', 'Kích thước archive vượt giới hạn server.', $request_id, 413 );
		}
		if ( $chunk_size < self::MIN_CHUNK_SIZE || $chunk_size > self::MAX_CHUNK_SIZE ) {
			return FYD_Unity_Response::error( 'invalid_chunk_size', 'Chunk size phải nằm trong khoảng 1-20 MiB.', $request_id, 422 );
		}
		$expected_chunks = (int) ceil( $archive_size / $chunk_size );
		if ( $total_chunks !== $expected_chunks || $total_chunks < 1 ) {
			return FYD_Unity_Response::error( 'invalid_chunk_count', 'Tổng số chunk không khớp archive size.', $request_id, 422 );
		}

		global $wpdb;
		$duplicate = $wpdb->get_var(
			$wpdb->prepare( "SELECT id FROM {$wpdb->prefix}fyd_unity_releases WHERE release_id = %s", $release_id )
		);
		if ( $duplicate ) {
			return FYD_Unity_Response::error( 'release_already_exists', 'Release ID đã tồn tại.', $request_id, 409 );
		}

		try {
			$this->storage->ensure_layout();
			$upload_id = bin2hex( random_bytes( 18 ) );
			$expires_at = time() + max( HOUR_IN_SECONDS, (int) get_option( 'fyd_unity_upload_ttl', self::DEFAULT_TTL ) );
			$session = array(
				'schemaVersion' => 1,
				'uploadId'      => $upload_id,
				'userId'        => get_current_user_id(),
				'appId'         => $app_id,
				'displayName'   => $display_name,
				'releaseId'     => $release_id,
				'releaseVersion'=> $release_version,
				'archiveSize'   => $archive_size,
				'archiveSha256' => $archive_hash,
				'chunkSize'     => $chunk_size,
				'totalChunks'   => $total_chunks,
				'receivedChunks'=> array(),
				'manifest'      => $this->sanitize_manifest( $params['manifest'] ?? array(), $app_id, $release_id, $archive_hash, $archive_size ),
				'createdAt'     => time(),
				'expiresAt'     => $expires_at,
			);
			$this->storage->create_session( $upload_id, $session );
			$app_db_id = $this->upsert_app( $app_id, $display_name );
			$this->logger->log( 'info', 'upload_initialized', 'Upload session initialized.', array( 'requestId' => $request_id, 'uploadId' => $upload_id, 'appId' => $app_id ), $app_db_id );
			return FYD_Unity_Response::success(
				array(
					'uploadId'   => $upload_id,
					'chunkSize'  => $chunk_size,
					'totalChunks'=> $total_chunks,
					'expiresAt'  => gmdate( 'c', $expires_at ),
				),
				$request_id,
				201
			);
		} catch ( Throwable $exception ) {
			$this->logger->log( 'error', 'upload_init_failed', 'Upload initialization failed.', array( 'requestId' => $request_id, 'code' => $exception->getCode() ) );
			return FYD_Unity_Response::error( 'upload_init_failed', 'Không tạo được upload session.', $request_id, 500 );
		}
	}

	public function receive_chunk( WP_REST_Request $request, string $upload_id, int $index, string $request_id ): WP_REST_Response {
		try {
			$lock = $this->storage->lock_session( $upload_id );
		} catch ( Throwable $exception ) {
			return FYD_Unity_Response::error( 'upload_not_found', 'Không tìm thấy upload session.', $request_id, 404 );
		}

		try {
			$session = $this->storage->read_session( $upload_id );
			$access  = $this->validate_session_access( $session, $request_id );
			if ( $access instanceof WP_REST_Response ) {
				return $access;
			}
			if ( $index < 0 || $index >= (int) $session['totalChunks'] ) {
				return FYD_Unity_Response::error( 'invalid_chunk_index', 'Chunk index ngoài phạm vi.', $request_id, 422 );
			}
			if ( (string) $request->get_header( 'x-fyd-app-id' ) !== (string) $session['appId'] ||
				(string) $request->get_header( 'x-fyd-upload-id' ) !== $upload_id ||
				(int) $request->get_header( 'x-fyd-chunk-index' ) !== $index ||
				(int) $request->get_header( 'x-fyd-total-chunks' ) !== (int) $session['totalChunks'] ) {
				return FYD_Unity_Response::error( 'chunk_metadata_mismatch', 'Metadata chunk không khớp session.', $request_id, 409 );
			}
			$bytes         = $request->get_body();
			$expected_size = min( (int) $session['chunkSize'], (int) $session['archiveSize'] - ( $index * (int) $session['chunkSize'] ) );
			if ( strlen( $bytes ) !== $expected_size ) {
				return FYD_Unity_Response::error( 'chunk_size_mismatch', 'Kích thước chunk không khớp.', $request_id, 422 );
			}
			$provided_hash = strtolower( (string) $request->get_header( 'x-fyd-chunk-sha256' ) );
			$actual_hash   = hash( 'sha256', $bytes );
			if ( ! preg_match( '/^[a-f0-9]{64}$/', $provided_hash ) || ! hash_equals( $provided_hash, $actual_hash ) ) {
				return FYD_Unity_Response::error( 'chunk_checksum_mismatch', 'Checksum của chunk không khớp.', $request_id, 422 );
			}

			$chunk_path = $this->storage->chunk_path( $upload_id, $index );
			if ( is_file( $chunk_path ) ) {
				$existing_hash = hash_file( 'sha256', $chunk_path );
				if ( hash_equals( $actual_hash, (string) $existing_hash ) ) {
					return FYD_Unity_Response::success( array( 'index' => $index, 'sha256' => $actual_hash, 'duplicate' => true ), $request_id );
				}
				return FYD_Unity_Response::error( 'chunk_conflict', 'Chunk index đã tồn tại với checksum khác.', $request_id, 409 );
			}

			$this->storage->atomic_write_bytes( $chunk_path, $bytes );
			$received = array_values( array_unique( array_map( 'intval', $session['receivedChunks'] ?? array() ) ) );
			$received[] = $index;
			sort( $received, SORT_NUMERIC );
			$session['receivedChunks'] = array_values( array_unique( $received ) );
			$this->storage->write_session( $upload_id, $session );
			$this->logger->log( 'info', 'chunk_received', 'Upload chunk received.', array( 'requestId' => $request_id, 'uploadId' => $upload_id, 'index' => $index ) );
			return FYD_Unity_Response::success( array( 'index' => $index, 'sha256' => $actual_hash, 'duplicate' => false ), $request_id );
		} catch ( Throwable $exception ) {
			return FYD_Unity_Response::error( 'chunk_write_failed', 'Không lưu được chunk.', $request_id, 500 );
		} finally {
			$this->storage->unlock_session( $lock );
		}
	}

	public function status( string $upload_id, string $request_id ): WP_REST_Response {
		try {
			$session = $this->storage->read_session( $upload_id );
		} catch ( Throwable $exception ) {
			$session = null;
		}
		$access = $this->validate_session_access( $session, $request_id );
		if ( $access instanceof WP_REST_Response ) {
			return $access;
		}
		$received = array_values( array_unique( array_map( 'intval', $session['receivedChunks'] ?? array() ) ) );
		sort( $received, SORT_NUMERIC );
		$missing = array_values( array_diff( range( 0, (int) $session['totalChunks'] - 1 ), $received ) );
		return FYD_Unity_Response::success(
			array(
				'uploadId'      => $upload_id,
				'totalChunks'   => (int) $session['totalChunks'],
				'receivedChunks'=> $received,
				'missingChunks' => $missing,
				'expiresAt'     => gmdate( 'c', (int) $session['expiresAt'] ),
			),
			$request_id
		);
	}

	public function cancel( string $upload_id, string $request_id ): WP_REST_Response {
		try {
			$session = $this->storage->read_session( $upload_id );
		} catch ( Throwable $exception ) {
			$session = null;
		}
		$access = $this->validate_session_access( $session, $request_id );
		if ( $access instanceof WP_REST_Response ) {
			return $access;
		}
		if ( ! $this->storage->delete_session( $upload_id ) ) {
			return FYD_Unity_Response::error( 'upload_delete_failed', 'Không xóa được upload session.', $request_id, 500 );
		}
		return FYD_Unity_Response::success( array( 'uploadId' => $upload_id, 'deleted' => true ), $request_id );
	}

	public function cleanup_expired(): int {
		$count = 0;
		$this->storage->ensure_layout();
		$directories = glob( $this->storage->temp_path() . DIRECTORY_SEPARATOR . '*' );
		foreach ( is_array( $directories ) ? $directories : array() as $directory ) {
			if ( ! is_dir( $directory ) ) {
				continue;
			}
			$upload_id = basename( $directory );
			try {
				$session = $this->storage->read_session( $upload_id );
				if ( ! is_array( $session ) || (int) ( $session['expiresAt'] ?? 0 ) < time() ) {
					if ( $this->storage->delete_session( $upload_id ) ) {
						++$count;
					}
				}
			} catch ( Throwable $exception ) {
				continue;
			}
		}
		$this->logger->log( 'info', 'cleanup_completed', 'Expired upload cleanup completed.', array( 'deleted' => $count ) );
		return $count;
	}

	private function validate_session_access( ?array $session, string $request_id ) {
		if ( ! is_array( $session ) ) {
			return FYD_Unity_Response::error( 'upload_not_found', 'Không tìm thấy upload session.', $request_id, 404 );
		}
		if ( (int) $session['expiresAt'] < time() ) {
			return FYD_Unity_Response::error( 'upload_expired', 'Upload session đã hết hạn.', $request_id, 410 );
		}
		if ( (int) $session['userId'] !== get_current_user_id() && ! current_user_can( FYD_Unity_Activator::CAP_MANAGE ) ) {
			return FYD_Unity_Response::error( 'upload_owner_mismatch', 'Upload session thuộc user khác.', $request_id, 403 );
		}
		return true;
	}

	private function sanitize_manifest( $manifest, string $app_id, string $release_id, string $archive_hash, int $archive_size ): array {
		$manifest = is_array( $manifest ) ? $manifest : array();
		$files    = array();
		foreach ( array_slice( is_array( $manifest['files'] ?? null ) ? $manifest['files'] : array(), 0, 500 ) as $file ) {
			if ( ! is_array( $file ) ) {
				continue;
			}
			$path = isset( $file['path'] ) ? str_replace( '\\', '/', (string) $file['path'] ) : '';
			$hash = isset( $file['sha256'] ) ? strtolower( (string) $file['sha256'] ) : '';
			if ( '' !== $path && ! str_starts_with( $path, '/' ) && ! str_contains( $path, '../' ) && preg_match( '/^[a-f0-9]{64}$/', $hash ) ) {
				$files[] = array( 'path' => sanitize_text_field( $path ), 'size' => max( 0, (int) ( $file['size'] ?? 0 ) ), 'sha256' => $hash );
			}
		}
		return array(
			'schemaVersion'  => 1,
			'appId'          => $app_id,
			'displayName'    => sanitize_text_field( (string) ( $manifest['displayName'] ?? '' ) ),
			'releaseVersion' => sanitize_text_field( (string) ( $manifest['releaseVersion'] ?? '' ) ),
			'releaseId'      => $release_id,
			'builtAtUtc'     => sanitize_text_field( (string) ( $manifest['builtAtUtc'] ?? '' ) ),
			'unityVersion'   => sanitize_text_field( (string) ( $manifest['unityVersion'] ?? '' ) ),
			'buildTarget'    => 'WebGL',
			'developmentBuild'=> ! empty( $manifest['developmentBuild'] ),
			'compression'    => sanitize_key( (string) ( $manifest['compression'] ?? '' ) ),
			'gitCommit'      => sanitize_text_field( (string) ( $manifest['gitCommit'] ?? '' ) ),
			'gitBranch'      => sanitize_text_field( (string) ( $manifest['gitBranch'] ?? '' ) ),
			'entryFile'      => 'index.html',
			'archiveSha256'  => $archive_hash,
			'archiveSize'    => $archive_size,
			'releaseNotes'   => sanitize_textarea_field( (string) ( $manifest['releaseNotes'] ?? '' ) ),
			'files'          => $files,
		);
	}

	private function upsert_app( string $app_id, string $display_name ): int {
		global $wpdb;
		$table = $wpdb->prefix . 'fyd_unity_apps';
		$id    = (int) $wpdb->get_var( $wpdb->prepare( "SELECT id FROM {$table} WHERE app_id = %s", $app_id ) );
		$now   = current_time( 'mysql', true );
		if ( $id ) {
			$wpdb->update( $table, array( 'display_name' => $display_name, 'updated_at' => $now ), array( 'id' => $id ), array( '%s', '%s' ), array( '%d' ) );
			return $id;
		}
		$wpdb->insert( $table, array( 'app_id' => $app_id, 'display_name' => $display_name, 'created_at' => $now, 'updated_at' => $now ), array( '%s', '%s', '%s', '%s' ) );
		return (int) $wpdb->insert_id;
	}
}
