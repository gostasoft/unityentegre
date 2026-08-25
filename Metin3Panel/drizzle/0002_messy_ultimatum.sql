CREATE TABLE `account_characters` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`account_id` integer NOT NULL,
	`name` text NOT NULL,
	`job` text DEFAULT 'Savaşçı' NOT NULL,
	`level` integer DEFAULT 1 NOT NULL,
	`empire` text DEFAULT 'Shinsoo' NOT NULL,
	`map_code` text DEFAULT 'metin2_map_a1' NOT NULL,
	`x` real DEFAULT 0 NOT NULL,
	`y` real DEFAULT 0 NOT NULL,
	`playtime` integer DEFAULT 0 NOT NULL,
	`online` integer DEFAULT false NOT NULL,
	`last_play` text
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_account_characters_name` ON `account_characters` (`name`);--> statement-breakpoint
CREATE INDEX `idx_account_characters_account` ON `account_characters` (`account_id`);--> statement-breakpoint
CREATE TABLE `accounts` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`login` text NOT NULL,
	`email` text DEFAULT '' NOT NULL,
	`status` text DEFAULT 'OK' NOT NULL,
	`empire` text DEFAULT '' NOT NULL,
	`created_at` text NOT NULL,
	`last_login` text
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_accounts_login` ON `accounts` (`login`);--> statement-breakpoint
CREATE TABLE `bans` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`target_type` text DEFAULT 'account' NOT NULL,
	`target` text NOT NULL,
	`reason` text DEFAULT '' NOT NULL,
	`expires_at` text,
	`active` integer DEFAULT true NOT NULL,
	`created_at` text NOT NULL
);
--> statement-breakpoint
CREATE INDEX `idx_bans_target` ON `bans` (`target_type`,`target`);--> statement-breakpoint
CREATE TABLE `biology_levels` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`level` integer NOT NULL,
	`item_vnum` integer NOT NULL,
	`item_count` integer DEFAULT 1 NOT NULL,
	`success_chance` real DEFAULT 100 NOT NULL,
	`cooldown_minutes` integer DEFAULT 1440 NOT NULL,
	`reward` text DEFAULT '' NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_biology_levels_level` ON `biology_levels` (`level`);--> statement-breakpoint
CREATE TABLE `chest_items` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`chest_vnum` integer NOT NULL,
	`item_vnum` integer NOT NULL,
	`item_name` text DEFAULT '' NOT NULL,
	`count` integer DEFAULT 1 NOT NULL,
	`chance` real DEFAULT 100 NOT NULL
);
--> statement-breakpoint
CREATE TABLE `chests` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`vnum` integer NOT NULL,
	`name` text NOT NULL,
	`roll_count` integer DEFAULT 1 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_chests_vnum` ON `chests` (`vnum`);--> statement-breakpoint
CREATE TABLE `exp_levels` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`level` integer NOT NULL,
	`required_exp` integer DEFAULT 0 NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_exp_levels_level` ON `exp_levels` (`level`);--> statement-breakpoint
CREATE TABLE `fishing_event_items` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`item_vnum` integer NOT NULL,
	`item_name` text DEFAULT '' NOT NULL,
	`chance` real DEFAULT 1 NOT NULL,
	`start_at` text,
	`end_at` text,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE TABLE `fishing_rates` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`fish_vnum` integer NOT NULL,
	`name` text NOT NULL,
	`chance` real DEFAULT 1 NOT NULL,
	`min_length` real DEFAULT 0 NOT NULL,
	`max_length` real DEFAULT 0 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE TABLE `gm_accounts` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`login` text NOT NULL,
	`character_name` text DEFAULT '' NOT NULL,
	`authority` text DEFAULT 'IMPLEMENTOR' NOT NULL,
	`contact_ip` text DEFAULT 'ALL' NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_gm_accounts_login` ON `gm_accounts` (`login`);--> statement-breakpoint
CREATE TABLE `market_items` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`market_id` integer NOT NULL,
	`item_vnum` integer NOT NULL,
	`item_name` text DEFAULT '' NOT NULL,
	`count` integer DEFAULT 1 NOT NULL,
	`price` integer DEFAULT 0 NOT NULL,
	`sold` integer DEFAULT false NOT NULL
);
--> statement-breakpoint
CREATE TABLE `markets` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`owner` text NOT NULL,
	`shop_name` text NOT NULL,
	`map_code` text DEFAULT '' NOT NULL,
	`x` real DEFAULT 0 NOT NULL,
	`y` real DEFAULT 0 NOT NULL,
	`created_at` text NOT NULL,
	`expires_at` text,
	`active` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE TABLE `proto_overrides` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`kind` text NOT NULL,
	`vnum` integer NOT NULL,
	`data` text NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_proto_overrides_kind_vnum` ON `proto_overrides` (`kind`,`vnum`);--> statement-breakpoint
CREATE TABLE `server_channels` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`name` text NOT NULL,
	`host` text DEFAULT '127.0.0.1' NOT NULL,
	`port` integer DEFAULT 0 NOT NULL,
	`status` text DEFAULT 'offline' NOT NULL,
	`players` integer DEFAULT 0 NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_server_channels_name` ON `server_channels` (`name`);--> statement-breakpoint
CREATE TABLE `trade_items` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`trade_id` integer NOT NULL,
	`direction` text DEFAULT 'giver' NOT NULL,
	`item_vnum` integer NOT NULL,
	`item_name` text DEFAULT '' NOT NULL,
	`count` integer DEFAULT 1 NOT NULL
);
--> statement-breakpoint
CREATE TABLE `trade_logs` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`giver` text NOT NULL,
	`receiver` text NOT NULL,
	`yang` integer DEFAULT 0 NOT NULL,
	`created_at` text NOT NULL,
	`ip_address` text DEFAULT '' NOT NULL
);
--> statement-breakpoint
CREATE TABLE `warp_categories` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`name` text NOT NULL,
	`position` integer DEFAULT 0 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE TABLE `warp_entries` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`category_id` integer NOT NULL,
	`name` text NOT NULL,
	`map_code` text NOT NULL,
	`x` real DEFAULT 0 NOT NULL,
	`y` real DEFAULT 0 NOT NULL,
	`min_level` integer DEFAULT 1 NOT NULL,
	`cost` integer DEFAULT 0 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
