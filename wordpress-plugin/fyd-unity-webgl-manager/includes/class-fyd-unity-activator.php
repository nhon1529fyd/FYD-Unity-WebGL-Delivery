<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Creates schema, capabilities and cleanup schedule. */
final class FYD_Unity_Activator {
	public const CAP_UPLOAD = 'upload_fyd_unity_builds';
	public const CAP_MANAGE = 'manage_fyd_unity_builds';
	public const CRON_HOOK  = 'fyd_unity_cleanup_expired_uploads';
	public const ERROR_OPTION = 'fyd_unity_activation_errors';

	public static function activate(): void {
		$errors = array();
		self::run_step( 'database', array( self::class, 'create_tables' ), $errors );
		self::run_step( 'capabilities', array( self::class, 'add_capabilities' ), $errors );
		self::run_step(
			'storage',
			static function (): void {
				$storage = new FYD_Unity_Storage();
				$storage->ensure_layout();
			},
			$errors
		);
		self::run_step(
			'cleanup_cron',
			static function (): void {
				if ( ! wp_next_scheduled( self::CRON_HOOK ) && ! wp_schedule_event( time() + HOUR_IN_SECONDS, 'daily', self::CRON_HOOK ) ) {
					throw new RuntimeException( 'Không đăng ký được cleanup cron.' );
				}
			},
			$errors
		);

		if ( $errors ) {
			update_option( self::ERROR_OPTION, $errors, false );
		} else {
			delete_option( self::ERROR_OPTION );
		}
		update_option( 'fyd_unity_db_version', FYD_UNITY_VERSION, false );
	}

	public static function deactivate(): void {
		wp_clear_scheduled_hook( self::CRON_HOOK );
	}

	public static function get_activation_errors(): array {
		$errors = get_option( self::ERROR_OPTION, array() );
		return is_array( $errors ) ? $errors : array();
	}

	private static function run_step( string $step, callable $callback, array &$errors ): void {
		try {
			$callback();
		} catch ( Throwable $exception ) {
			$errors[] = array(
				'step'    => sanitize_key( $step ),
				'message' => sanitize_text_field( $exception->getMessage() ),
			);
		}
	}

	private static function add_capabilities(): void {
		$role = get_role( 'administrator' );
		if ( $role ) {
			$role->add_cap( self::CAP_UPLOAD );
			$role->add_cap( self::CAP_MANAGE );
		}
		$deployer = add_role(
			'fyd_unity_deployer',
			'FYD Unity Deployer',
			array(
				'read'           => true,
				self::CAP_UPLOAD => true,
			)
		);
		if ( ! $deployer ) {
			$deployer = get_role( 'fyd_unity_deployer' );
		}
		if ( $deployer ) {
			$deployer->add_cap( 'read' );
			$deployer->add_cap( self::CAP_UPLOAD );
		}
	}

	private static function create_tables(): void {
		global $wpdb;
		require_once ABSPATH . 'wp-admin/includes/upgrade.php';
		$charset = $wpdb->get_charset_collate();
		$apps     = $wpdb->prefix . 'fyd_unity_apps';
		$releases = $wpdb->prefix . 'fyd_unity_releases';
		$logs     = $wpdb->prefix . 'fyd_unity_logs';

		dbDelta( "CREATE TABLE {$apps} (
			id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
			app_id varchar(100) NOT NULL,
			display_name varchar(255) NOT NULL,
			active_release_id bigint(20) unsigned NULL,
			created_at datetime NOT NULL,
			updated_at datetime NOT NULL,
			settings_json longtext NULL,
			PRIMARY KEY  (id),
			UNIQUE KEY app_id (app_id)
		) {$charset};" );

		dbDelta( "CREATE TABLE {$releases} (
			id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
			app_db_id bigint(20) unsigned NOT NULL,
			release_id varchar(190) NOT NULL,
			version varchar(100) NOT NULL,
			status varchar(30) NOT NULL,
			storage_path text NULL,
			entry_file varchar(255) NOT NULL DEFAULT 'index.html',
			archive_sha256 char(64) NOT NULL,
			archive_size bigint(20) unsigned NOT NULL DEFAULT 0,
			unity_version varchar(100) NULL,
			git_commit varchar(100) NULL,
			manifest_json longtext NULL,
			release_notes longtext NULL,
			created_by bigint(20) unsigned NOT NULL,
			created_at datetime NOT NULL,
			activated_at datetime NULL,
			error_code varchar(100) NULL,
			error_message text NULL,
			PRIMARY KEY  (id),
			UNIQUE KEY release_id (release_id),
			KEY app_status (app_db_id,status)
		) {$charset};" );

		dbDelta( "CREATE TABLE {$logs} (
			id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
			app_db_id bigint(20) unsigned NULL,
			release_db_id bigint(20) unsigned NULL,
			level varchar(20) NOT NULL,
			event varchar(100) NOT NULL,
			message text NOT NULL,
			context_json longtext NULL,
			user_id bigint(20) unsigned NULL,
			created_at datetime NOT NULL,
			PRIMARY KEY  (id),
			KEY app_created (app_db_id,created_at),
			KEY release_created (release_db_id,created_at)
		) {$charset};" );
	}
}
