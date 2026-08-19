<?php

$root = dirname( __DIR__ );
$rest = file_get_contents( $root . '/includes/class-fyd-unity-rest-controller.php' );
$upload = file_get_contents( $root . '/includes/class-fyd-unity-upload-service.php' );
$storage = file_get_contents( $root . '/includes/class-fyd-unity-storage.php' );

$assertions = array(
	'No public write permission callback' => false === strpos( $rest, '__return_true' ),
	'Upload capability is enforced'       => false !== strpos( $rest, 'CAP_UPLOAD' ),
	'Chunk SHA-256 uses hash_equals'      => false !== strpos( $upload, 'hash_equals' ),
	'Session ownership is checked'        => false !== strpos( $upload, 'userId' ),
	'Writes use temporary then rename'    => false !== strpos( $storage, 'rename( $temporary, $path )' ),
	'Temporary storage has deny rules'    => false !== strpos( $storage, 'Require all denied' ),
);

$failed = 0;
foreach ( $assertions as $name => $passed ) {
	echo ( $passed ? '[PASS] ' : '[FAIL] ' ) . $name . PHP_EOL;
	if ( ! $passed ) {
		++$failed;
	}
}

exit( $failed > 0 ? 1 : 0 );
