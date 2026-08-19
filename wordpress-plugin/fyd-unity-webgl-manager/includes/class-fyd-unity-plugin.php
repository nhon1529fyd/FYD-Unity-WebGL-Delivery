<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Composes plugin services and WordPress hooks. */
final class FYD_Unity_Plugin {
	private FYD_Unity_Upload_Service $uploads;

	public function __construct() {
		$storage       = new FYD_Unity_Storage();
		$logger        = new FYD_Unity_Logger();
		$this->uploads = new FYD_Unity_Upload_Service( $storage, $logger );
		$health        = new FYD_Unity_Health_Check( $storage );
		$rest          = new FYD_Unity_REST_Controller( $this->uploads, $health );
		add_action( 'rest_api_init', array( $rest, 'register_routes' ) );
	}

	public function run(): void {
		if ( FYD_UNITY_VERSION !== get_option( 'fyd_unity_db_version' ) && current_user_can( 'activate_plugins' ) ) {
			FYD_Unity_Activator::activate();
		}
		add_action( FYD_Unity_Activator::CRON_HOOK, array( $this->uploads, 'cleanup_expired' ) );
		add_action( 'admin_notices', array( $this, 'activation_notice' ) );
	}

	public function activation_notice(): void {
		if ( ! current_user_can( 'activate_plugins' ) ) {
			return;
		}
		$errors = FYD_Unity_Activator::get_activation_errors();
		if ( ! $errors ) {
			return;
		}
		echo '<div class="notice notice-error"><p><strong>' . esc_html__( 'FYD Unity setup chưa hoàn tất:', 'fyd-unity-webgl-manager' ) . '</strong></p><ul>';
		foreach ( $errors as $error ) {
			echo '<li>' . esc_html( (string) ( $error['step'] ?? 'setup' ) ) . ': ' . esc_html( (string) ( $error['message'] ?? '' ) ) . '</li>';
		}
		echo '</ul></div>';
	}
}
