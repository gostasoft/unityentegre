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

export const protoOverrides = sqliteTable('proto_overrides', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  kind: text('kind').notNull(),
  vnum: integer('vnum').notNull(),
  data: text('data').notNull(),
  updatedAt: text('updated_at').notNull(),
}, (table) => [uniqueIndex('idx_proto_overrides_kind_vnum').on(table.kind, table.vnum)]);

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
  yang: integer('yang').notNull().default(0),
  won: integer('won').notNull().default(0),
  lastMapCode: text('last_map_code').notNull().default(''),
  lastX: real('last_x').notNull().default(0),
  lastY: real('last_y').notNull().default(0),
  lastIp: text('last_ip').notNull().default(''),
  hwid: text('hwid').notNull().default(''),
  pcId: text('pc_id').notNull().default(''),
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

export const accounts = sqliteTable('accounts', {
  id: integer('id').primaryKey({ autoIncrement: true }),
  login: text('login').notNull(), email: text('email').notNull().default(''),
  status: text('status').notNull().default('OK'), empire: text('empire').notNull().default(''),
  createdAt: text('created_at').notNull(), lastLogin: text('last_login'),
}, (table) => [uniqueIndex('idx_accounts_login').on(table.login)]);

export const accountCharacters = sqliteTable('account_characters', {
  id: integer('id').primaryKey({ autoIncrement: true }), accountId: integer('account_id').notNull(),
  name: text('name').notNull(), job: text('job').notNull().default('Savaşçı'), level: integer('level').notNull().default(1),
  empire: text('empire').notNull().default('Shinsoo'), mapCode: text('map_code').notNull().default('metin2_map_a1'),
  x: real('x').notNull().default(0), y: real('y').notNull().default(0), playtime: integer('playtime').notNull().default(0),
  online: integer('online', { mode: 'boolean' }).notNull().default(false), lastPlay: text('last_play'),
}, (table) => [uniqueIndex('idx_account_characters_name').on(table.name), index('idx_account_characters_account').on(table.accountId)]);

export const gmAccounts = sqliteTable('gm_accounts', {
  id: integer('id').primaryKey({ autoIncrement: true }), login: text('login').notNull(), characterName: text('character_name').notNull().default(''),
  authority: text('authority').notNull().default('IMPLEMENTOR'), contactIp: text('contact_ip').notNull().default('ALL'), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true),
}, (table) => [uniqueIndex('idx_gm_accounts_login').on(table.login)]);

export const bans = sqliteTable('bans', {
  id: integer('id').primaryKey({ autoIncrement: true }), targetType: text('target_type').notNull().default('account'), target: text('target').notNull(),
  reason: text('reason').notNull().default(''), expiresAt: text('expires_at'), active: integer('active', { mode: 'boolean' }).notNull().default(true), createdAt: text('created_at').notNull(),
}, (table) => [index('idx_bans_target').on(table.targetType, table.target)]);

export const warpCategories = sqliteTable('warp_categories', { id: integer('id').primaryKey({ autoIncrement: true }), name: text('name').notNull(), position: integer('position').notNull().default(0), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) });
export const warpEntries = sqliteTable('warp_entries', { id: integer('id').primaryKey({ autoIncrement: true }), categoryId: integer('category_id').notNull(), name: text('name').notNull(), mapCode: text('map_code').notNull(), x: real('x').notNull().default(0), y: real('y').notNull().default(0), minLevel: integer('min_level').notNull().default(1), cost: integer('cost').notNull().default(0), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) });
export const expLevels = sqliteTable('exp_levels', { id: integer('id').primaryKey({ autoIncrement: true }), level: integer('level').notNull(), requiredExp: integer('required_exp').notNull().default(0) }, (table) => [uniqueIndex('idx_exp_levels_level').on(table.level)]);
export const biologyLevels = sqliteTable('biology_levels', { id: integer('id').primaryKey({ autoIncrement: true }), level: integer('level').notNull(), questName: text('quest_name').notNull().default(''), giverName: text('giver_name').notNull().default('Biyolog Chaegirab'), itemVnum: integer('item_vnum').notNull(), itemCount: integer('item_count').notNull().default(1), soulItemVnum: integer('soul_item_vnum'), successChance: real('success_chance').notNull().default(100), cooldownMinutes: integer('cooldown_minutes').notNull().default(1440), reward: text('reward').notNull().default(''), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) }, (table) => [uniqueIndex('idx_biology_levels_level').on(table.level)]);
export const biologyRewards = sqliteTable('biology_rewards', { id: integer('id').primaryKey({ autoIncrement: true }), biologyLevel: integer('biology_level').notNull(), choiceGroup: integer('choice_group').notNull().default(0), rewardType: text('reward_type').notNull().default('stat'), rewardKey: text('reward_key').notNull().default(''), rewardValue: real('reward_value').notNull().default(0), itemVnum: integer('item_vnum'), itemCount: integer('item_count').notNull().default(1), label: text('label').notNull().default(''), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) }, (table) => [index('idx_biology_rewards_level').on(table.biologyLevel)]);
export const playerInventory = sqliteTable('player_inventory', { id: integer('id').primaryKey({ autoIncrement: true }), playerId: integer('player_id').notNull(), slot: integer('slot').notNull(), itemVnum: integer('item_vnum').notNull(), itemName: text('item_name').notNull().default(''), count: integer('count').notNull().default(1), equipped: integer('equipped', { mode: 'boolean' }).notNull().default(false), sockets: text('sockets').notNull().default('[]'), attributes: text('attributes').notNull().default('[]'), updatedAt: text('updated_at').notNull() }, (table) => [uniqueIndex('idx_player_inventory_slot').on(table.playerId, table.slot)]);
export const playerLocationHistory = sqliteTable('player_location_history', { id: integer('id').primaryKey({ autoIncrement: true }), playerId: integer('player_id').notNull(), mapCode: text('map_code').notNull(), x: real('x').notNull().default(0), y: real('y').notNull().default(0), enteredAt: text('entered_at').notNull(), leftAt: text('left_at'), durationSeconds: integer('duration_seconds').notNull().default(0), stationarySeconds: integer('stationary_seconds').notNull().default(0) }, (table) => [index('idx_player_location_history').on(table.playerId, table.enteredAt)]);
export const playerSanctions = sqliteTable('player_sanctions', { id: integer('id').primaryKey({ autoIncrement: true }), playerId: integer('player_id').notNull(), sanctionType: text('sanction_type').notNull(), targetValue: text('target_value').notNull(), reason: text('reason').notNull().default(''), startsAt: text('starts_at').notNull(), expiresAt: text('expires_at'), active: integer('active', { mode: 'boolean' }).notNull().default(true), createdAt: text('created_at').notNull(), createdBy: text('created_by').notNull().default('') }, (table) => [index('idx_player_sanctions_active').on(table.playerId, table.active)]);
export const chests = sqliteTable('chests', { id: integer('id').primaryKey({ autoIncrement: true }), vnum: integer('vnum').notNull(), name: text('name').notNull(), rollCount: integer('roll_count').notNull().default(1), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) }, (table) => [uniqueIndex('idx_chests_vnum').on(table.vnum)]);
export const chestItems = sqliteTable('chest_items', { id: integer('id').primaryKey({ autoIncrement: true }), chestVnum: integer('chest_vnum').notNull(), itemVnum: integer('item_vnum').notNull(), itemName: text('item_name').notNull().default(''), count: integer('count').notNull().default(1), chance: real('chance').notNull().default(100) });
export const fishingRates = sqliteTable('fishing_rates', { id: integer('id').primaryKey({ autoIncrement: true }), fishVnum: integer('fish_vnum').notNull(), name: text('name').notNull(), chance: real('chance').notNull().default(1), minLength: real('min_length').notNull().default(0), maxLength: real('max_length').notNull().default(0), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) });
export const fishingEventItems = sqliteTable('fishing_event_items', { id: integer('id').primaryKey({ autoIncrement: true }), itemVnum: integer('item_vnum').notNull(), itemName: text('item_name').notNull().default(''), chance: real('chance').notNull().default(1), startAt: text('start_at'), endAt: text('end_at'), enabled: integer('enabled', { mode: 'boolean' }).notNull().default(true) });
export const markets = sqliteTable('markets', { id: integer('id').primaryKey({ autoIncrement: true }), owner: text('owner').notNull(), shopName: text('shop_name').notNull(), mapCode: text('map_code').notNull().default(''), x: real('x').notNull().default(0), y: real('y').notNull().default(0), createdAt: text('created_at').notNull(), expiresAt: text('expires_at'), active: integer('active', { mode: 'boolean' }).notNull().default(true) });
export const marketItems = sqliteTable('market_items', { id: integer('id').primaryKey({ autoIncrement: true }), marketId: integer('market_id').notNull(), itemVnum: integer('item_vnum').notNull(), itemName: text('item_name').notNull().default(''), count: integer('count').notNull().default(1), price: integer('price').notNull().default(0), sold: integer('sold', { mode: 'boolean' }).notNull().default(false) });
export const tradeLogs = sqliteTable('trade_logs', { id: integer('id').primaryKey({ autoIncrement: true }), giver: text('giver').notNull(), receiver: text('receiver').notNull(), yang: integer('yang').notNull().default(0), createdAt: text('created_at').notNull(), ipAddress: text('ip_address').notNull().default('') });
export const tradeItems = sqliteTable('trade_items', { id: integer('id').primaryKey({ autoIncrement: true }), tradeId: integer('trade_id').notNull(), direction: text('direction').notNull().default('giver'), itemVnum: integer('item_vnum').notNull(), itemName: text('item_name').notNull().default(''), count: integer('count').notNull().default(1) });
export const serverChannels = sqliteTable('server_channels', { id: integer('id').primaryKey({ autoIncrement: true }), name: text('name').notNull(), host: text('host').notNull().default('127.0.0.1'), port: integer('port').notNull().default(0), status: text('status').notNull().default('offline'), players: integer('players').notNull().default(0), updatedAt: text('updated_at').notNull() }, (table) => [uniqueIndex('idx_server_channels_name').on(table.name)]);
