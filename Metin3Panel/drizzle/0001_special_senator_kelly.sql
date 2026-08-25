CREATE TABLE `world_placements` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`map_id` integer NOT NULL,
	`target_kind` text NOT NULL,
	`target_vnum` integer NOT NULL,
	`x` real NOT NULL,
	`y` real NOT NULL,
	`z` real DEFAULT 0 NOT NULL,
	`direction` real DEFAULT 0 NOT NULL,
	`radius` real DEFAULT 0 NOT NULL,
	`respawn_seconds` integer DEFAULT 60 NOT NULL,
	`count` integer DEFAULT 1 NOT NULL,
	`enabled` integer DEFAULT true NOT NULL,
	`updated_at` text NOT NULL,
	FOREIGN KEY (`map_id`) REFERENCES `maps`(`id`) ON UPDATE no action ON DELETE cascade
);
--> statement-breakpoint
CREATE INDEX `idx_world_placements_map` ON `world_placements` (`map_id`);--> statement-breakpoint
CREATE INDEX `idx_world_placements_target` ON `world_placements` (`target_kind`,`target_vnum`);