<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Owns all filesystem paths and atomic writes below the FYD uploads root. */
final class FYD_Unity_Storage {
	private string $root;

	public function __construct() {
		$uploads    = wp_upload_dir();
		$this->root = trailingslashit( $uploads['basedir'] ) . 'fyd-unity';
	}

	public function ensure_layout(): void {
		foreach ( array( $this->root, $this->apps_path(), $this->temp_path(), $this->logs_path() ) as $path ) {
			if ( ! wp_mkdir_p( $path ) ) {
				throw new RuntimeException( 'Không tạo được thư mục FYD Unity.' );
			}
		}
		$this->protect_directory( $this->temp_path() );
		$this->protect_directory( $this->logs_path() );
	}

	public function is_writable(): bool {
		return is_dir( $this->root ) && is_writable( $this->root );
	}

	public function apps_path(): string {
		return $this->root . DIRECTORY_SEPARATOR . 'apps';
	}

	public function temp_path(): string {
		return $this->root . DIRECTORY_SEPARATOR . 'temp';
	}

	public function logs_path(): string {
		return $this->root . DIRECTORY_SEPARATOR . 'logs';
	}

	public function session_path( string $upload_id ): string {
		if ( ! preg_match( '/^[a-f0-9]{36}$/', $upload_id ) ) {
			throw new InvalidArgumentException( 'Upload ID không hợp lệ.' );
		}
		return $this->temp_path() . DIRECTORY_SEPARATOR . $upload_id;
	}

	public function create_session( string $upload_id, array $session ): void {
		$path = $this->session_path( $upload_id );
		if ( file_exists( $path ) || ! wp_mkdir_p( $path . DIRECTORY_SEPARATOR . 'chunks' ) ) {
			throw new RuntimeException( 'Không tạo được upload session.' );
		}
		$this->protect_directory( $path );
		$this->atomic_write_json( $path . DIRECTORY_SEPARATOR . 'upload.json', $session );
	}

	public function read_session( string $upload_id ): ?array {
		$file = $this->session_path( $upload_id ) . DIRECTORY_SEPARATOR . 'upload.json';
		if ( ! is_file( $file ) ) {
			return null;
		}
		$data = json_decode( (string) file_get_contents( $file ), true );
		return is_array( $data ) ? $data : null;
	}

	public function write_session( string $upload_id, array $session ): void {
		$this->atomic_write_json( $this->session_path( $upload_id ) . DIRECTORY_SEPARATOR . 'upload.json', $session );
	}

	public function lock_session( string $upload_id ) {
		$lock = fopen( $this->session_path( $upload_id ) . DIRECTORY_SEPARATOR . 'session.lock', 'c' );
		if ( false === $lock || ! flock( $lock, LOCK_EX ) ) {
			throw new RuntimeException( 'Không khóa được upload session.' );
		}
		return $lock;
	}

	public function unlock_session( $lock ): void {
		if ( is_resource( $lock ) ) {
			flock( $lock, LOCK_UN );
			fclose( $lock );
		}
	}

	public function chunk_path( string $upload_id, int $index ): string {
		if ( $index < 0 ) {
			throw new InvalidArgumentException( 'Chunk index không hợp lệ.' );
		}
		return $this->session_path( $upload_id ) . DIRECTORY_SEPARATOR . 'chunks' . DIRECTORY_SEPARATOR . $index . '.part';
	}

	public function atomic_write_bytes( string $path, string $bytes ): void {
		$temporary = $path . '.tmp-' . bin2hex( random_bytes( 6 ) );
		$handle    = fopen( $temporary, 'xb' );
		if ( false === $handle ) {
			throw new RuntimeException( 'Không tạo được file tạm.' );
		}
		try {
			$written = fwrite( $handle, $bytes );
			if ( false === $written || $written !== strlen( $bytes ) ) {
				throw new RuntimeException( 'Không ghi đủ dữ liệu chunk.' );
			}
			fflush( $handle );
		} finally {
			fclose( $handle );
		}
		if ( ! rename( $temporary, $path ) ) {
			@unlink( $temporary );
			throw new RuntimeException( 'Không hoàn tất ghi file atomic.' );
		}
	}

	public function delete_session( string $upload_id ): bool {
		return $this->delete_tree_below_root( $this->session_path( $upload_id ) );
	}

	private function atomic_write_json( string $path, array $data ): void {
		$json = wp_json_encode( $data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE );
		if ( false === $json ) {
			throw new RuntimeException( 'Không encode được upload metadata.' );
		}
		$this->atomic_write_bytes( $path, $json );
	}

	private function protect_directory( string $path ): void {
		$index = $path . DIRECTORY_SEPARATOR . 'index.php';
		if ( ! file_exists( $index ) ) {
			file_put_contents( $index, "<?php\n// Silence is golden.\n" );
		}
		$deny = $path . DIRECTORY_SEPARATOR . '.htaccess';
		if ( ! file_exists( $deny ) ) {
			file_put_contents( $deny, "Options -Indexes\n<IfModule mod_authz_core.c>\nRequire all denied\n</IfModule>\n<IfModule !mod_authz_core.c>\nDeny from all\n</IfModule>\n" );
		}
		$web_config = $path . DIRECTORY_SEPARATOR . 'web.config';
		if ( ! file_exists( $web_config ) ) {
			file_put_contents( $web_config, "<?xml version=\"1.0\" encoding=\"UTF-8\"?><configuration><system.webServer><security><authorization><remove users=\"*\" roles=\"\" verbs=\"\"/><add accessType=\"Deny\" users=\"*\"/></authorization></security></system.webServer></configuration>" );
		}
	}

	private function delete_tree_below_root( string $target ): bool {
		$root_real   = realpath( $this->temp_path() );
		$target_real = realpath( $target );
		if ( false === $root_real || false === $target_real || 0 !== strpos( $target_real, $root_real . DIRECTORY_SEPARATOR ) ) {
			return false;
		}
		$iterator = new RecursiveIteratorIterator(
			new RecursiveDirectoryIterator( $target_real, FilesystemIterator::SKIP_DOTS ),
			RecursiveIteratorIterator::CHILD_FIRST
		);
		foreach ( $iterator as $item ) {
			$item->isDir() && ! $item->isLink() ? rmdir( $item->getPathname() ) : unlink( $item->getPathname() );
		}
		return rmdir( $target_real );
	}
}
