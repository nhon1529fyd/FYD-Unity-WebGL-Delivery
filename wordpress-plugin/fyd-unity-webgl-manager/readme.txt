=== FYD Unity WebGL Manager ===
Contributors: fyd
Requires at least: 6.0
Requires PHP: 8.0
Stable tag: 0.2.1
License: GPLv2 or later

Securely receives versioned Unity WebGL archives through authenticated chunk uploads.

== Installation ==

1. Upload and activate the plugin ZIP.
2. Create a user with the FYD Unity Deployer role.
3. Create a WordPress Application Password for that user.
4. Configure the HTTPS site URL and credential in FYD Unity Publisher.

== Changelog ==

= 0.2.0 =
* Added schema, capabilities, health/status API and resumable chunk uploads.

= 0.2.1 =
* Hardened activation so individual setup failures are reported without taking down WordPress admin.
