import { index, integer, real, sqliteTable, text, uniqueIndex } from 'drizzle-orm/sqlite-core';

export const maps = sqliteTable('maps', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  code: text('code').notNull(),
  name: text('name').notNull(),
  width: integer('width').notNull().default(1024),
  height: integer('height').notNull().default(1024),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
}, (table) => [uniqueIndex('idx_maps_code').on(table.code)]);

export const entities = sqliteTable('entities', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  vnum: integer('vnum').notNull(),
  name: text('name').notNull(),
  type: text('type', { enum: ['mob', 'metin', 'npc'] }).notNull(),
  rank: text('rank').notNull().default('Normal'),
  level: integer('level').notNull().default(1),
  hp: integer('hp').notNull().default(100),
  exp: integer('exp').notNull().default(0),
  minDamage: integer('min_damage').notNull().default(1),
  maxDamage: integer('max_damage').notNull().default(2),
  defense: integer('defense').notNull().default(0),
  attackSpeed: integer('attack_speed').notNull().default(100),
  moveSpeed: integer('move_speed').notNull().default(100),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
  updatedAt: text('updated_at').notNull(),
}, (table) => [uniqueIndex('idx_entities_vnum').on(table.vnum), index('idx_entities_type').on(table.type)]);

export const items = sqliteTable('items', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  vnum: integer('vnum').notNull(),
  name: text('name').notNull(),
  category: text('category').notNull().default('Diğer'),
  buyPrice: integer('buy_price').notNull().default(0),
  sellPrice: integer('sell_price').notNull().default(0),
  stackable: integer('stackable', { mode: 'boolean' }).notNull().default(false),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
  updatedAt: text('updated_at').notNull(),
}, (table) => [uniqueIndex('idx_items_vnum').on(table.vnum), index('idx_items_category').on(table.category)]);

export const spawns = sqliteTable('spawns', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  mapId: integer('map_id').notNull().references(() => maps.id, { onDelete: 'cascade' }),
  entityId: integer('entity_id').notNull().references(() => entities.id, { onDelete: 'cascade' }),
  x: real('x').notNull(), y: real('y').notNull(), z: real('z').notNull().default(0),
  direction: real('direction').notNull().default(0),
  respawnSeconds: integer('respawn_seconds').notNull().default(60),
  groupSize: integer('group_size').notNull().default(1),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
}, (table) => [index('idx_spawns_map').on(table.mapId), index('idx_spawns_entity').on(table.entityId)]);

export const worldPlacements = sqliteTable('world_placements', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  mapId: integer('map_id').notNull().references(() => maps.id, { onDelete: 'cascade' }),
  targetKind: text('target_kind', { enum: ['mob', 'metin', 'group'] }).notNull(),
  targetVnum: integer('target_vnum').notNull(),
  x: real('x').notNull(),
  y: real('y').notNull(),
  z: real('z').notNull().default(0),
  direction: real('direction').notNull().default(0),
  radius: real('radius').notNull().default(0),
  respawnSeconds: integer('respawn_seconds').notNull().default(60),
  count: integer('count').notNull().default(1),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
  updatedAt: text('updated_at').notNull(),
}, (table) => [
  index('idx_world_placements_map').on(table.mapId),
  index('idx_world_placements_target').on(table.targetKind, table.targetVnum),
]);

export const drops = sqliteTable('drops', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  entityId: integer('entity_id').notNull().references(() => entities.id, { onDelete: 'cascade' }),
  itemId: integer('item_id').notNull().references(() => items.id, { onDelete: 'cascade' }),
  chance: real('chance').notNull().default(1),
  minCount: integer('min_count').notNull().default(1),
  maxCount: integer('max_count').notNull().default(1),
  minLevel: integer('min_level').notNull().default(1),
  maxLevel: integer('max_level').notNull().default(120),
}, (table) => [index('idx_drops_entity').on(table.entityId)]);

export const shops = sqliteTable('shops', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  entityId: integer('entity_id').notNull().references(() => entities.id, { onDelete: 'cascade' }),
  name: text('name').notNull(),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
}, (table) => [index('idx_shops_entity').on(table.entityId)]);

export const shopItems = sqliteTable('shop_items', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  shopId: integer('shop_id').notNull().references(() => shops.id, { onDelete: 'cascade' }),
  itemId: integer('item_id').notNull().references(() => items.id, { onDelete: 'cascade' }),
  buyPrice: integer('buy_price').notNull(),
  sellPrice: integer('sell_price').notNull(),
  position: integer('position').notNull().default(0),
}, (table) => [index('idx_shop_items_shop').on(table.shopId)]);

export const players = sqliteTable('players', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  account: text('account').notNull(),
  characterName: text('character_name').notNull(),
  empire: text('empire').notNull().default('Shinsoo'),
  characterClass: text('character_class').notNull().default('Savaşçı'),
  level: integer('level').notNull().default(1),
  online: integer('online', { mode: 'boolean' }).notNull().default(false),
  lastSeen: text('last_seen').notNull(),
  banUntil: text('ban_until'),
  banReason: text('ban_reason'),
  muteUntil: text('mute_until'),
}, (table) => [uniqueIndex('idx_players_character').on(table.characterName), index('idx_players_account').on(table.account)]);

export const events = sqliteTable('events', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  name: text('name').notNull(),
  description: text('description').notNull().default(''),
  targetType: text('target_type').notNull().default('all'),
  startAt: text('start_at').notNull(),
  endAt: text('end_at').notNull(),
  multiplier: real('multiplier').notNull().default(1),
  enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
}, (table) => [index('idx_events_schedule').on(table.startAt, table.endAt)]);

export const settings = sqliteTable('settings', {
  key: text('key').primaryKey(),
  value: text('value').notNull(),
  updatedAt: text('updated_at').notNull(),
});

export const auditLogs = sqliteTable('audit_logs', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  actor: text('actor').notNull(),
  action: text('action').notNull(),
  resource: text('resource').notNull(),
  resourceId: text('resource_id'),
  summary: text('summary').notNull(),
  createdAt: text('created_at').notNull(),
}, (table) => [index('idx_audit_created').on(table.createdAt)]);
