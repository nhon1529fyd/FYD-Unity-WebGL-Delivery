<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Performs non-mutating server checks relevant to chunked WebGL delivery. */
final class FYD_Unity_Health_Check {
	private FYD_Unity_Storage $storage;

	public function __construct( FYD_Unity_Storage $storage ) {
		$this->storage = $storage;
	}

	public function run(): array {
		$checks = array();
		$checks[] = $this->check( 'https', is_ssl(), is_ssl() ? 'pass' : 'fail', is_ssl() ? 'Website đang dùng HTTPS.' : 'Publisher yêu cầu HTTPS.' );
		try {
			$this->storage->ensure_layout();
			$writable = $this->storage->is_writable();
		} catch ( Throwable $exception ) {
			$writable = false;
		}
		$checks[] = $this->check( 'storage_writable', $writable, $writable ? 'pass' : 'fail', $writable ? 'FYD upload storage ghi được.' : 'FYD upload storage không ghi được.' );
		$checks[] = $this->check( 'ziparchive', class_exists( 'ZipArchive' ), class_exists( 'ZipArchive' ) ? 'pass' : 'fail', class_exists( 'ZipArchive' ) ? 'PHP ZipArchive khả dụng.' : 'Cần bật PHP ZIP extension trước finalize.' );
		$cron = (bool) wp_next_scheduled( FYD_Unity_Activator::CRON_HOOK );
		$checks[] = $this->check( 'cleanup_cron', $cron, $cron ? 'pass' : 'warning', $cron ? 'Cleanup cron đã đăng ký.' : 'Cleanup cron chưa được đăng ký.' );
		$checks[] = array(
			'id'      => 'php_limits',
			'status'  => 'pass',
			'message' => sprintf( 'upload_max_filesize=%s, post_max_size=%s', ini_get( 'upload_max_filesize' ), ini_get( 'post_max_size' ) ),
		);
		$checks[] = $this->check( 'application_passwords', class_exists( 'WP_Application_Passwords' ), class_exists( 'WP_Application_Passwords' ) ? 'pass' : 'fail', class_exists( 'WP_Application_Passwords' ) ? 'WordPress Application Password khả dụng.' : 'WordPress Application Password không khả dụng.' );
		return $checks;
	}

	private function check( string $id, bool $condition, string $status, string $message ): array {
		return array( 'id' => $id, 'status' => $condition ? $status : $status, 'message' => $message );
	}
}
