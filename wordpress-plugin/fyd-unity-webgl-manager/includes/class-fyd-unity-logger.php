<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Writes redacted deployment events without credentials or raw request data. */
final class FYD_Unity_Logger {
	public function log(
		string $level,
		string $event,
		string $message,
		array $context = array(),
		?int $app_db_id = null,
		?int $release_db_id = null
	): void {
		global $wpdb;
		$blocked = array( 'authorization', 'password', 'application_password', 'token', 'credential' );
		foreach ( $blocked as $key ) {
			unset( $context[ $key ] );
		}
		$wpdb->insert(
			$wpdb->prefix . 'fyd_unity_logs',
			array(
				'app_db_id'    => $app_db_id,
				'release_db_id'=> $release_db_id,
				'level'        => sanitize_key( $level ),
				'event'        => sanitize_key( $event ),
				'message'      => sanitize_text_field( $message ),
				'context_json' => wp_json_encode( $context, JSON_UNESCAPED_SLASHES ),
				'user_id'      => get_current_user_id() ?: null,
				'created_at'   => current_time( 'mysql', true ),
			),
			array( '%d', '%d', '%s', '%s', '%s', '%s', '%d', '%s' )
		);
	}
}
