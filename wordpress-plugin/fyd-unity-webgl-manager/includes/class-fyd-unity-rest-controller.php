<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Registers versioned REST routes and enforces FYD capabilities. */
final class FYD_Unity_REST_Controller {
	private const NAMESPACE = 'fyd-unity/v1';
	private FYD_Unity_Upload_Service $uploads;
	private FYD_Unity_Health_Check $health;

	public function __construct( FYD_Unity_Upload_Service $uploads, FYD_Unity_Health_Check $health ) {
		$this->uploads = $uploads;
		$this->health  = $health;
	}

	public function register_routes(): void {
		register_rest_route(
			self::NAMESPACE,
			'/status',
			array(
				'methods'             => WP_REST_Server::READABLE,
				'callback'            => array( $this, 'status' ),
				'permission_callback' => array( $this, 'can_upload' ),
			)
		);
		register_rest_route(
			self::NAMESPACE,
			'/health',
			array(
				'methods'             => WP_REST_Server::READABLE,
				'callback'            => array( $this, 'health' ),
				'permission_callback' => array( $this, 'can_manage' ),
			)
		);
		register_rest_route(
			self::NAMESPACE,
			'/apps',
			array(
				'methods'             => WP_REST_Server::READABLE,
				'callback'            => array( $this, 'apps' ),
				'permission_callback' => array( $this, 'can_upload' ),
			)
		);
		register_rest_route(
			self::NAMESPACE,
			'/uploads/init',
			array(
				'methods'             => WP_REST_Server::CREATABLE,
				'callback'            => array( $this, 'initialize_upload' ),
				'permission_callback' => array( $this, 'can_upload' ),
			)
		);
		register_rest_route(
			self::NAMESPACE,
			'/uploads/(?P<upload_id>[a-f0-9]{36})/chunks/(?P<index>\d+)',
			array(
				'methods'             => 'PUT',
				'callback'            => array( $this, 'receive_chunk' ),
				'permission_callback' => array( $this, 'can_upload' ),
			)
		);
		register_rest_route(
			self::NAMESPACE,
			'/uploads/(?P<upload_id>[a-f0-9]{36})',
			array(
				array(
					'methods'             => WP_REST_Server::READABLE,
					'callback'            => array( $this, 'upload_status' ),
					'permission_callback' => array( $this, 'can_upload' ),
				),
				array(
					'methods'             => WP_REST_Server::DELETABLE,
					'callback'            => array( $this, 'cancel_upload' ),
					'permission_callback' => array( $this, 'can_upload' ),
				),
			)
		);
	}

	public function can_upload(): bool {
		return current_user_can( FYD_Unity_Activator::CAP_UPLOAD );
	}

	public function can_manage(): bool {
		return current_user_can( FYD_Unity_Activator::CAP_MANAGE );
	}

	public function status(): WP_REST_Response {
		$user = wp_get_current_user();
		return FYD_Unity_Response::success(
			array( 'pluginVersion' => FYD_UNITY_VERSION, 'apiVersion' => 'v1', 'user' => $user->user_login ),
			FYD_Unity_Response::request_id()
		);
	}

	public function health(): WP_REST_Response {
		return FYD_Unity_Response::success( array( 'checks' => $this->health->run() ), FYD_Unity_Response::request_id() );
	}

	public function apps(): WP_REST_Response {
		global $wpdb;
		$rows = $wpdb->get_results(
			"SELECT app_id, display_name, active_release_id, created_at, updated_at FROM {$wpdb->prefix}fyd_unity_apps ORDER BY updated_at DESC",
			ARRAY_A
		);
		return FYD_Unity_Response::success( array( 'apps' => is_array( $rows ) ? $rows : array() ), FYD_Unity_Response::request_id() );
	}

	public function initialize_upload( WP_REST_Request $request ): WP_REST_Response {
		return $this->uploads->initialize( $request, FYD_Unity_Response::request_id() );
	}

	public function receive_chunk( WP_REST_Request $request ): WP_REST_Response {
		return $this->uploads->receive_chunk( $request, (string) $request['upload_id'], (int) $request['index'], FYD_Unity_Response::request_id() );
	}

	public function upload_status( WP_REST_Request $request ): WP_REST_Response {
		return $this->uploads->status( (string) $request['upload_id'], FYD_Unity_Response::request_id() );
	}

	public function cancel_upload( WP_REST_Request $request ): WP_REST_Response {
		return $this->uploads->cancel( (string) $request['upload_id'], FYD_Unity_Response::request_id() );
	}
}
