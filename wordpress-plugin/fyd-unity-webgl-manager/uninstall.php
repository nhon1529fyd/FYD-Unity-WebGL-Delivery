<?php

if ( ! defined( 'WP_UNINSTALL_PLUGIN' ) ) {
	exit;
}

wp_clear_scheduled_hook( 'fyd_unity_cleanup_expired_uploads' );
$role = get_role( 'administrator' );
if ( $role ) {
	$role->remove_cap( 'upload_fyd_unity_builds' );
	$role->remove_cap( 'manage_fyd_unity_builds' );
}
remove_role( 'fyd_unity_deployer' );

// Release records and files are intentionally retained to prevent accidental data loss.
