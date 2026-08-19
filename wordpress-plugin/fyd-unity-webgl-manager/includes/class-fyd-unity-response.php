<?php

if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/** Standard FYD REST response envelope. */
final class FYD_Unity_Response {
	public static function request_id(): string {
		return 'fyd-' . wp_generate_password( 16, false, false );
	}

	public static function success( array $data, string $request_id, int $status = 200 ): WP_REST_Response {
		return new WP_REST_Response(
			array(
				'ok'        => true,
				'data'      => $data,
				'requestId' => $request_id,
			),
			$status
		);
	}

	public static function error(
		string $code,
		string $message,
		string $request_id,
		int $status = 400,
		array $details = array()
	): WP_REST_Response {
		return new WP_REST_Response(
			array(
				'ok'        => false,
				'error'     => array(
					'code'    => sanitize_key( $code ),
					'message' => $message,
					'details' => $details,
				),
				'requestId' => $request_id,
			),
			$status
		);
	}
}
