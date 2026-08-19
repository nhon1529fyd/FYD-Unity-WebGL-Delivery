<?php
/**
 * Plugin Name: FYD Unity WebGL Manager
 * Description: Receives versioned Unity WebGL releases through a secure chunked REST API.
 * Version: 0.2.1
 * Requires at least: 6.0
 * Requires PHP: 8.0
 * Author: FYD
 * Text Domain: fyd-unity-webgl-manager
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

define( 'FYD_UNITY_VERSION', '0.2.1' );
define( 'FYD_UNITY_PLUGIN_FILE', __FILE__ );
define( 'FYD_UNITY_PLUGIN_DIR', plugin_dir_path( __FILE__ ) );

require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-response.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-storage.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-logger.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-activator.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-upload-service.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-health-check.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-rest-controller.php';
require_once FYD_UNITY_PLUGIN_DIR . 'includes/class-fyd-unity-plugin.php';

register_activation_hook( __FILE__, array( 'FYD_Unity_Activator', 'activate' ) );
register_deactivation_hook( __FILE__, array( 'FYD_Unity_Activator', 'deactivate' ) );

add_action(
	'plugins_loaded',
	static function (): void {
		$plugin = new FYD_Unity_Plugin();
		$plugin->run();
	}
);
