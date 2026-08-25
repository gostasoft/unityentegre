CREATE TABLE `audit_logs` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`actor` text NOT NULL,
	`action` text NOT NULL,
	`resource` text NOT NULL,
	`resource_id` text,
	`summary` text NOT NULL,
	`created_at` text NOT NULL
);
--> statement-breakpoint
CREATE INDEX `idx_audit_created` ON `audit_logs` (`created_at`);--> statement-breakpoint
CREATE TABLE `drops` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`entity_id` integer NOT NULL,
	`item_id` integer NOT NULL,
	`chance` real DEFAULT 1 NOT NULL,
	`min_count` integer DEFAULT 1 NOT NULL,
	`max_count` integer DEFAULT 1 NOT NULL,
	`min_level` integer DEFAULT 1 NOT NULL,
	`max_level` integer DEFAULT 120 NOT NULL,
	FOREIGN KEY (`entity_id`) REFERENCES `entities`(`id`) ON UPDATE no action ON DELETE cascade,
	FOREIGN KEY (`item_id`) REFERENCES `items`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `idx_drops_entity` ON `drops` (`entity_id`);--> statement-breakpoint
CREATE TABLE `entities` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`vnum` integer NOT NULL,
	`name` text NOT NULL,
	`type` text NOT NULL,
	`rank` text DEFAULT 'Normal' NOT NULL,
	`level` integer DEFAULT 1 NOT NULL,
	`hp` integer DEFAULT 100 NOT NULL,
	`exp` integer DEFAULT 0 NOT NULL,
	`min_damage` integer DEFAULT 1 NOT NULL,
	`max_damage` integer DEFAULT 2 NOT NULL,
	`defense` integer DEFAULT 0 NOT NULL,
	`attack_speed` integer DEFAULT 100 NOT NULL,
	`move_speed` integer DEFAULT 100 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_entities_vnum` ON `entities` (`vnum`);--> statement-breakpoint
CREATE INDEX `idx_entities_type` ON `entities` (`type`);--> statement-breakpoint
CREATE TABLE `events` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`name` text NOT NULL,
	`description` text DEFAULT '' NOT NULL,
	`target_type` text DEFAULT 'all' NOT NULL,
	`start_at` text NOT NULL,
	`end_at` text NOT NULL,
	`multiplier` real DEFAULT 1 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE INDEX `idx_events_schedule` ON `events` (`start_at`,`end_at`);--> statement-breakpoint
CREATE TABLE `items` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`vnum` integer NOT NULL,
	`name` text NOT NULL,
	`category` text DEFAULT 'Diğer' NOT NULL,
	`buy_price` integer DEFAULT 0 NOT NULL,
	`sell_price` integer DEFAULT 0 NOT NULL,
	`stackable` integer DEFAULT false NOT NULL,
	`enabled` integer DEFAULT true NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_items_vnum` ON `items` (`vnum`);--> statement-breakpoint
CREATE INDEX `idx_items_category` ON `items` (`category`);--> statement-breakpoint
CREATE TABLE `maps` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`code` text NOT NULL,
	`name` text NOT NULL,
	`width` integer DEFAULT 1024 NOT NULL,
	`height` integer DEFAULT 1024 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_maps_code` ON `maps` (`code`);--> statement-breakpoint
CREATE TABLE `players` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`account` text NOT NULL,
	`character_name` text NOT NULL,
	`empire` text DEFAULT 'Shinsoo' NOT NULL,
	`character_class` text DEFAULT 'Savaşçı' NOT NULL,
	`level` integer DEFAULT 1 NOT NULL,
	`online` integer DEFAULT false NOT NULL,
	`last_seen` text NOT NULL,
	`ban_until` text,
	`ban_reason` text,
	`mute_until` text
);
--> statement-breakpoint
CREATE UNIQUE INDEX `idx_players_character` ON `players` (`character_name`);--> statement-breakpoint
CREATE INDEX `idx_players_account` ON `players` (`account`);--> statement-breakpoint
CREATE TABLE `settings` (
	`key` text PRIMARY KEY NOT NULL,
	`value` text NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE TABLE `shop_items` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`shop_id` integer NOT NULL,
	`item_id` integer NOT NULL,
	`buy_price` integer NOT NULL,
	`sell_price` integer NOT NULL,
	`position` integer DEFAULT 0 NOT NULL,
	FOREIGN KEY (`shop_id`) REFERENCES `shops`(`id`) ON UPDATE no action ON DELETE cascade,
	FOREIGN KEY (`item_id`) REFERENCES `items`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `idx_shop_items_shop` ON `shop_items` (`shop_id`);--> statement-breakpoint
CREATE TABLE `shops` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`entity_id` integer NOT NULL,
	`name` text NOT NULL,
	`enabled` integer DEFAULT true NOT NULL,
	FOREIGN KEY (`entity_id`) REFERENCES `entities`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `idx_shops_entity` ON `shops` (`entity_id`);--> statement-breakpoint
CREATE TABLE `spawns` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`map_id` integer NOT NULL,
	`entity_id` integer NOT NULL,
	`x` real NOT NULL,
	`y` real NOT NULL,
	`z` real DEFAULT 0 NOT NULL,
	`direction` real DEFAULT 0 NOT NULL,
	`respawn_seconds` integer DEFAULT 60 NOT NULL,
	`group_size` integer DEFAULT 1 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL,
	FOREIGN KEY (`map_id`) REFERENCES `maps`(`id`) ON UPDATE no action ON DELETE cascade,
	FOREIGN KEY (`entity_id`) REFERENCES `entities`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `idx_spawns_map` ON `spawns` (`map_id`);--> statement-breakpoint
CREATE INDEX `idx_spawns_entity` ON `spawns` (`entity_id`);