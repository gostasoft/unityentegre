import { env } from 'cloudflare:workers';
import originalSpawnsJson from '../data/original-spawns.json';

const schemaStatements = [
  `CREATE TABLE IF NOT EXISTS maps (id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, width INTEGER NOT NULL DEFAULT 1024, height INTEGER NOT NULL DEFAULT 1024, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS entities (id INTEGER PRIMARY KEY AUTOINCREMENT, vnum INTEGER NOT NULL UNIQUE, name TEXT NOT NULL, type TEXT NOT NULL, rank TEXT NOT NULL DEFAULT 'Normal', level INTEGER NOT NULL DEFAULT 1, hp INTEGER NOT NULL DEFAULT 100, exp INTEGER NOT NULL DEFAULT 0, min_damage INTEGER NOT NULL DEFAULT 1, max_damage INTEGER NOT NULL DEFAULT 2, defense INTEGER NOT NULL DEFAULT 0, attack_speed INTEGER NOT NULL DEFAULT 100, move_speed INTEGER NOT NULL DEFAULT 100, enabled INTEGER NOT NULL DEFAULT 1, updated_at TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS items (id INTEGER PRIMARY KEY AUTOINCREMENT, vnum INTEGER NOT NULL UNIQUE, name TEXT NOT NULL, category TEXT NOT NULL DEFAULT 'Diğer', buy_price INTEGER NOT NULL DEFAULT 0, sell_price INTEGER NOT NULL DEFAULT 0, stackable INTEGER NOT NULL DEFAULT 0, enabled INTEGER NOT NULL DEFAULT 1, updated_at TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS spawns (id INTEGER PRIMARY KEY AUTOINCREMENT, map_id INTEGER NOT NULL REFERENCES maps(id) ON DELETE CASCADE, entity_id INTEGER NOT NULL REFERENCES entities(id) ON DELETE CASCADE, x REAL NOT NULL, y REAL NOT NULL, z REAL NOT NULL DEFAULT 0, direction REAL NOT NULL DEFAULT 0, respawn_seconds INTEGER NOT NULL DEFAULT 60, group_size INTEGER NOT NULL DEFAULT 1, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS world_placements (id INTEGER PRIMARY KEY AUTOINCREMENT, map_id INTEGER NOT NULL REFERENCES maps(id) ON DELETE CASCADE, target_kind TEXT NOT NULL, target_vnum INTEGER NOT NULL, x REAL NOT NULL, y REAL NOT NULL, z REAL NOT NULL DEFAULT 0, direction REAL NOT NULL DEFAULT 0, radius REAL NOT NULL DEFAULT 0, spread_x REAL NOT NULL DEFAULT 0, spread_y REAL NOT NULL DEFAULT 0, spawn_percent REAL NOT NULL DEFAULT 100, respawn_seconds INTEGER NOT NULL DEFAULT 60, count INTEGER NOT NULL DEFAULT 1, enabled INTEGER NOT NULL DEFAULT 1, source_key TEXT, updated_at TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS proto_overrides (id INTEGER PRIMARY KEY AUTOINCREMENT, kind TEXT NOT NULL, vnum INTEGER NOT NULL, data TEXT NOT NULL, updated_at TEXT NOT NULL, UNIQUE(kind,vnum))`,
  `CREATE TABLE IF NOT EXISTS drops (id INTEGER PRIMARY KEY AUTOINCREMENT, entity_id INTEGER NOT NULL REFERENCES entities(id) ON DELETE CASCADE, item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE, chance REAL NOT NULL DEFAULT 1, min_count INTEGER NOT NULL DEFAULT 1, max_count INTEGER NOT NULL DEFAULT 1, min_level INTEGER NOT NULL DEFAULT 1, max_level INTEGER NOT NULL DEFAULT 120)`,
  `CREATE TABLE IF NOT EXISTS shops (id INTEGER PRIMARY KEY AUTOINCREMENT, entity_id INTEGER NOT NULL REFERENCES entities(id) ON DELETE CASCADE, name TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS shop_items (id INTEGER PRIMARY KEY AUTOINCREMENT, shop_id INTEGER NOT NULL REFERENCES shops(id) ON DELETE CASCADE, item_id INTEGER NOT NULL REFERENCES items(id) ON DELETE CASCADE, buy_price INTEGER NOT NULL, sell_price INTEGER NOT NULL, position INTEGER NOT NULL DEFAULT 0)`,
  `CREATE TABLE IF NOT EXISTS players (id INTEGER PRIMARY KEY AUTOINCREMENT, account TEXT NOT NULL, character_name TEXT NOT NULL UNIQUE, empire TEXT NOT NULL DEFAULT 'Shinsoo', character_class TEXT NOT NULL DEFAULT 'Savaşçı', level INTEGER NOT NULL DEFAULT 1, online INTEGER NOT NULL DEFAULT 0, last_seen TEXT NOT NULL, ban_until TEXT, ban_reason TEXT, mute_until TEXT)`,
  `CREATE TABLE IF NOT EXISTS events (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, description TEXT NOT NULL DEFAULT '', target_type TEXT NOT NULL DEFAULT 'all', start_at TEXT NOT NULL, end_at TEXT NOT NULL, multiplier REAL NOT NULL DEFAULT 1, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL, updated_at TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS audit_logs (id INTEGER PRIMARY KEY AUTOINCREMENT, actor TEXT NOT NULL, action TEXT NOT NULL, resource TEXT NOT NULL, resource_id TEXT, summary TEXT NOT NULL, created_at TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS accounts (id INTEGER PRIMARY KEY AUTOINCREMENT, login TEXT NOT NULL UNIQUE, email TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'OK', empire TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL, last_login TEXT)`,
  `CREATE TABLE IF NOT EXISTS account_characters (id INTEGER PRIMARY KEY AUTOINCREMENT, account_id INTEGER NOT NULL, name TEXT NOT NULL UNIQUE, job TEXT NOT NULL DEFAULT 'Savaşçı', level INTEGER NOT NULL DEFAULT 1, empire TEXT NOT NULL DEFAULT 'Shinsoo', map_code TEXT NOT NULL DEFAULT 'metin2_map_a1', x REAL NOT NULL DEFAULT 0, y REAL NOT NULL DEFAULT 0, playtime INTEGER NOT NULL DEFAULT 0, online INTEGER NOT NULL DEFAULT 0, last_play TEXT)`,
  `CREATE TABLE IF NOT EXISTS gm_accounts (id INTEGER PRIMARY KEY AUTOINCREMENT, login TEXT NOT NULL UNIQUE, character_name TEXT NOT NULL DEFAULT '', authority TEXT NOT NULL DEFAULT 'IMPLEMENTOR', contact_ip TEXT NOT NULL DEFAULT 'ALL', enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS bans (id INTEGER PRIMARY KEY AUTOINCREMENT, target_type TEXT NOT NULL DEFAULT 'account', target TEXT NOT NULL, reason TEXT NOT NULL DEFAULT '', expires_at TEXT, active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL)`,
  `CREATE TABLE IF NOT EXISTS warp_categories (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, position INTEGER NOT NULL DEFAULT 0, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS warp_entries (id INTEGER PRIMARY KEY AUTOINCREMENT, category_id INTEGER NOT NULL, name TEXT NOT NULL, map_code TEXT NOT NULL, x REAL NOT NULL DEFAULT 0, y REAL NOT NULL DEFAULT 0, min_level INTEGER NOT NULL DEFAULT 1, cost INTEGER NOT NULL DEFAULT 0, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS exp_levels (id INTEGER PRIMARY KEY AUTOINCREMENT, level INTEGER NOT NULL UNIQUE, required_exp INTEGER NOT NULL DEFAULT 0)`,
  `CREATE TABLE IF NOT EXISTS biology_levels (id INTEGER PRIMARY KEY AUTOINCREMENT, level INTEGER NOT NULL UNIQUE, quest_name TEXT NOT NULL DEFAULT '', giver_name TEXT NOT NULL DEFAULT 'Biyolog Chaegirab', item_vnum INTEGER NOT NULL, item_count INTEGER NOT NULL DEFAULT 1, soul_item_vnum INTEGER, success_chance REAL NOT NULL DEFAULT 100, cooldown_minutes INTEGER NOT NULL DEFAULT 1440, reward TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS biology_rewards (id INTEGER PRIMARY KEY AUTOINCREMENT, biology_level INTEGER NOT NULL, choice_group INTEGER NOT NULL DEFAULT 0, reward_type TEXT NOT NULL DEFAULT 'stat', reward_key TEXT NOT NULL DEFAULT '', reward_value REAL NOT NULL DEFAULT 0, item_vnum INTEGER, item_count INTEGER NOT NULL DEFAULT 1, label TEXT NOT NULL DEFAULT '', enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS player_inventory (id INTEGER PRIMARY KEY AUTOINCREMENT, player_id INTEGER NOT NULL, slot INTEGER NOT NULL, item_vnum INTEGER NOT NULL, item_name TEXT NOT NULL DEFAULT '', count INTEGER NOT NULL DEFAULT 1, equipped INTEGER NOT NULL DEFAULT 0, sockets TEXT NOT NULL DEFAULT '[]', attributes TEXT NOT NULL DEFAULT '[]', updated_at TEXT NOT NULL, UNIQUE(player_id,slot))`,
  `CREATE TABLE IF NOT EXISTS player_location_history (id INTEGER PRIMARY KEY AUTOINCREMENT, player_id INTEGER NOT NULL, map_code TEXT NOT NULL, x REAL NOT NULL DEFAULT 0, y REAL NOT NULL DEFAULT 0, entered_at TEXT NOT NULL, left_at TEXT, duration_seconds INTEGER NOT NULL DEFAULT 0, stationary_seconds INTEGER NOT NULL DEFAULT 0)`,
  `CREATE TABLE IF NOT EXISTS player_sanctions (id INTEGER PRIMARY KEY AUTOINCREMENT, player_id INTEGER NOT NULL, sanction_type TEXT NOT NULL, target_value TEXT NOT NULL, reason TEXT NOT NULL DEFAULT '', starts_at TEXT NOT NULL, expires_at TEXT, active INTEGER NOT NULL DEFAULT 1, created_at TEXT NOT NULL, created_by TEXT NOT NULL DEFAULT '')`,
  `CREATE TABLE IF NOT EXISTS chests (id INTEGER PRIMARY KEY AUTOINCREMENT, vnum INTEGER NOT NULL UNIQUE, name TEXT NOT NULL, roll_count INTEGER NOT NULL DEFAULT 1, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS chest_items (id INTEGER PRIMARY KEY AUTOINCREMENT, chest_vnum INTEGER NOT NULL, item_vnum INTEGER NOT NULL, item_name TEXT NOT NULL DEFAULT '', count INTEGER NOT NULL DEFAULT 1, chance REAL NOT NULL DEFAULT 100)`,
  `CREATE TABLE IF NOT EXISTS fishing_rates (id INTEGER PRIMARY KEY AUTOINCREMENT, fish_vnum INTEGER NOT NULL, name TEXT NOT NULL, chance REAL NOT NULL DEFAULT 1, min_length REAL NOT NULL DEFAULT 0, max_length REAL NOT NULL DEFAULT 0, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS fishing_event_items (id INTEGER PRIMARY KEY AUTOINCREMENT, item_vnum INTEGER NOT NULL, item_name TEXT NOT NULL DEFAULT '', chance REAL NOT NULL DEFAULT 1, start_at TEXT, end_at TEXT, enabled INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS markets (id INTEGER PRIMARY KEY AUTOINCREMENT, owner TEXT NOT NULL, shop_name TEXT NOT NULL, map_code TEXT NOT NULL DEFAULT '', x REAL NOT NULL DEFAULT 0, y REAL NOT NULL DEFAULT 0, created_at TEXT NOT NULL, expires_at TEXT, active INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS market_items (id INTEGER PRIMARY KEY AUTOINCREMENT, market_id INTEGER NOT NULL, item_vnum INTEGER NOT NULL, item_name TEXT NOT NULL DEFAULT '', count INTEGER NOT NULL DEFAULT 1, price INTEGER NOT NULL DEFAULT 0, sold INTEGER NOT NULL DEFAULT 0)`,
  `CREATE TABLE IF NOT EXISTS trade_logs (id INTEGER PRIMARY KEY AUTOINCREMENT, giver TEXT NOT NULL, receiver TEXT NOT NULL, yang INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, ip_address TEXT NOT NULL DEFAULT '')`,
  `CREATE TABLE IF NOT EXISTS trade_items (id INTEGER PRIMARY KEY AUTOINCREMENT, trade_id INTEGER NOT NULL, direction TEXT NOT NULL DEFAULT 'giver', item_vnum INTEGER NOT NULL, item_name TEXT NOT NULL DEFAULT '', count INTEGER NOT NULL DEFAULT 1)`,
  `CREATE TABLE IF NOT EXISTS server_channels (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE, host TEXT NOT NULL DEFAULT '127.0.0.1', port INTEGER NOT NULL DEFAULT 0, status TEXT NOT NULL DEFAULT 'offline', players INTEGER NOT NULL DEFAULT 0, updated_at TEXT NOT NULL)`,
  `CREATE INDEX IF NOT EXISTS idx_entities_type ON entities(type)`,
  `CREATE INDEX IF NOT EXISTS idx_spawns_map ON spawns(map_id)`,
  `CREATE INDEX IF NOT EXISTS idx_world_placements_map ON world_placements(map_id)`,
  `CREATE INDEX IF NOT EXISTS idx_world_placements_target ON world_placements(target_kind,target_vnum)`,
  `CREATE INDEX IF NOT EXISTS idx_drops_entity ON drops(entity_id)`,
  `CREATE INDEX IF NOT EXISTS idx_shop_items_shop ON shop_items(shop_id)`,
  `CREATE INDEX IF NOT EXISTS idx_events_schedule ON events(start_at, end_at)`,
  `CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_logs(created_at)`,
  `CREATE INDEX IF NOT EXISTS idx_biology_rewards_level ON biology_rewards(biology_level)`,
  `CREATE INDEX IF NOT EXISTS idx_player_location_history ON player_location_history(player_id,entered_at)`,
  `CREATE INDEX IF NOT EXISTS idx_player_sanctions_active ON player_sanctions(player_id,active)`,
];

const requiredColumns: Record<string, Array<[string,string]>> = {
  biology_levels: [['quest_name',"TEXT NOT NULL DEFAULT ''"],['giver_name',"TEXT NOT NULL DEFAULT 'Biyolog Chaegirab'"],['soul_item_vnum','INTEGER']],
  players: [['yang','INTEGER NOT NULL DEFAULT 0'],['won','INTEGER NOT NULL DEFAULT 0'],['last_map_code',"TEXT NOT NULL DEFAULT ''"],['last_x','REAL NOT NULL DEFAULT 0'],['last_y','REAL NOT NULL DEFAULT 0'],['last_ip',"TEXT NOT NULL DEFAULT ''"],['hwid',"TEXT NOT NULL DEFAULT ''"],['pc_id',"TEXT NOT NULL DEFAULT ''"]],
  world_placements: [['spread_x','REAL NOT NULL DEFAULT 0'],['spread_y','REAL NOT NULL DEFAULT 0'],['spawn_percent','REAL NOT NULL DEFAULT 100'],['source_key','TEXT']],
};

export function database(): D1Database {
  if (!env.DB) throw new Error('DB bağlantısı bulunamadı.');
  return env.DB;
}

export async function ensureDatabase() {
  const db = database();
  await db.batch(schemaStatements.map((sql) => db.prepare(sql)));
  for (const [table, columns] of Object.entries(requiredColumns)) {
    const existing = await db.prepare(`PRAGMA table_info(${table})`).all<{ name:string }>();
    const names = new Set((existing.results ?? []).map((column) => column.name));
    for (const [name, definition] of columns) if (!names.has(name)) await db.prepare(`ALTER TABLE ${table} ADD COLUMN ${name} ${definition}`).run();
  }
  await db.prepare('CREATE UNIQUE INDEX IF NOT EXISTS idx_world_placements_source ON world_placements(source_key)').run();
  await db.prepare('PRAGMA optimize').run();
  const advancedCount = await db.prepare('SELECT COUNT(*) AS count FROM exp_levels').first<{ count: number }>();
  if (Number(advancedCount?.count ?? 0) === 0) {
    const now = new Date().toISOString();
    const channels = ['CH1','CH2','CH3','CH4','CH5','CH6','CH99','AUTH','DB'];
    await db.batch([
      ...Array.from({ length: 120 }, (_, index) => {
        const level = index + 1;
        const required = level === 1 ? 0 : Math.round(100 * Math.pow(level, 2.45));
        return db.prepare('INSERT OR IGNORE INTO exp_levels (level,required_exp) VALUES (?,?)').bind(level, required);
      }),
      db.prepare('INSERT INTO warp_categories (name,position,enabled) SELECT ?,0,1 WHERE NOT EXISTS (SELECT 1 FROM warp_categories)').bind('Başlangıç Haritaları'),
      ...channels.map((name, index) => db.prepare('INSERT OR IGNORE INTO server_channels (name,host,port,status,players,updated_at) VALUES (?,?,?,?,0,?)').bind(name, '127.0.0.1', 13000 + index * 10, 'offline', now)),
    ]);
  }
  await seedBiology(db);
  const count = await db.prepare('SELECT COUNT(*) AS count FROM maps').first<{ count: number }>();
  if (Number(count?.count ?? 0) > 0) {
    await migrateLegacyPlacements(db);
    await seedOriginalWorld(db);
    return;
  }

  const now = new Date().toISOString();
  await db.batch([
    db.prepare('INSERT INTO maps (code,name,width,height,enabled) VALUES (?,?,?,?,1)').bind('metin2_map_c1','Jinno Birinci Köy',1024,1024),
    db.prepare('INSERT INTO maps (code,name,width,height,enabled) VALUES (?,?,?,?,1)').bind('metin2_map_b1','Chunjo Birinci Köy',1024,1024),
    db.prepare('INSERT INTO maps (code,name,width,height,enabled) VALUES (?,?,?,?,1)').bind('metin2_map_a1','Shinsoo Birinci Köy',1024,1024),
    db.prepare('INSERT INTO maps (code,name,width,height,enabled) VALUES (?,?,?,?,1)').bind('spider_dungeon_01','Örümcek Zindanı 1',2048,2048),
    db.prepare('INSERT INTO maps (code,name,width,height,enabled) VALUES (?,?,?,?,1)').bind('metin2_map_milgyo','Tapınak',1536,1536),
    db.prepare(`INSERT INTO entities (vnum,name,type,rank,level,hp,exp,min_damage,max_damage,defense,attack_speed,move_speed,enabled,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,1,?)`).bind(101,'Vahşi Köpek','mob','Normal',1,120,32,5,9,2,100,120,now),
    db.prepare(`INSERT INTO entities (vnum,name,type,rank,level,hp,exp,min_damage,max_damage,defense,attack_speed,move_speed,enabled,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,1,?)`).bind(591,'Vahşi Yüzbaşı','mob','Patron',42,285000,14800,630,920,380,115,105,now),
    db.prepare(`INSERT INTO entities (vnum,name,type,rank,level,hp,exp,min_damage,max_damage,defense,attack_speed,move_speed,enabled,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,1,?)`).bind(8005,'Kıskançlık Metini','metin','Metin',35,250000,8400,480,720,290,85,0,now),
    db.prepare(`INSERT INTO entities (vnum,name,type,rank,level,hp,exp,min_damage,max_damage,defense,attack_speed,move_speed,enabled,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,1,?)`).bind(9001,'Silah Satıcısı','npc','NPC',1,1,0,0,0,0,0,0,now),
    db.prepare(`INSERT INTO entities (vnum,name,type,rank,level,hp,exp,min_damage,max_damage,defense,attack_speed,move_speed,enabled,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,1,?)`).bind(9003,'Genel Satıcı','npc','NPC',1,1,0,0,0,0,0,0,now),
    db.prepare('INSERT INTO items (vnum,name,category,buy_price,sell_price,stackable,enabled,updated_at) VALUES (?,?,?,?,?,0,1,?)').bind(10,'Kılıç +0','Silah',1500,250,now),
    db.prepare('INSERT INTO items (vnum,name,category,buy_price,sell_price,stackable,enabled,updated_at) VALUES (?,?,?,?,?,0,1,?)').bind(299,'Dolunay Kılıcı','Silah',250000,50000,now),
    db.prepare('INSERT INTO items (vnum,name,category,buy_price,sell_price,stackable,enabled,updated_at) VALUES (?,?,?,?,?,1,1,?)').bind(27001,'Kırmızı İksir (K)','İksir',120,20,now),
    db.prepare('INSERT INTO items (vnum,name,category,buy_price,sell_price,stackable,enabled,updated_at) VALUES (?,?,?,?,?,1,1,?)').bind(50011,'Ay Işığı Define Sandığı','Sandık',0,1000,now),
    db.prepare('INSERT INTO players (account,character_name,empire,character_class,level,online,last_seen) VALUES (?,?,?,?,?,?,?)').bind('salih','Alp','Jinno','Savaşçı',75,1,now),
    db.prepare('INSERT INTO players (account,character_name,empire,character_class,level,online,last_seen) VALUES (?,?,?,?,?,?,?)').bind('test_ninja','Nyx','Shinsoo','Ninja',42,0,now),
    ...[['exp_rate','1.00'],['drop_rate','1.00'],['yang_rate','1.00'],['mob_hp_rate','1.00'],['mob_damage_rate','1.00'],['server_maintenance','false']].map(([key,value]) => db.prepare('INSERT INTO settings (key,value,updated_at) VALUES (?,?,?)').bind(key,value,now)),
  ]);

  const ids = await Promise.all([
    db.prepare('SELECT id FROM maps WHERE code=?').bind('metin2_map_c1').first<{id:number}>(),
    db.prepare('SELECT id FROM entities WHERE vnum=?').bind(591).first<{id:number}>(),
    db.prepare('SELECT id FROM entities WHERE vnum=?').bind(8005).first<{id:number}>(),
    db.prepare('SELECT id FROM entities WHERE vnum=?').bind(9001).first<{id:number}>(),
    db.prepare('SELECT id FROM items WHERE vnum=?').bind(299).first<{id:number}>(),
    db.prepare('SELECT id FROM items WHERE vnum=?').bind(10).first<{id:number}>(),
  ]);
  const [map, captain, metin, npc, moonSword, sword] = ids;
  if (!map || !captain || !metin || !npc || !moonSword || !sword) return;
  const shop = await db.prepare('INSERT INTO shops (entity_id,name,enabled) VALUES (?,?,1) RETURNING id').bind(npc.id,'Silah Satıcısı').first<{id:number}>();
  await db.batch([
    db.prepare('INSERT INTO spawns (map_id,entity_id,x,y,z,direction,respawn_seconds,group_size,enabled) VALUES (?,?,?,?,?,?,?,?,1)').bind(map.id,captain.id,382,614,0,90,180,1),
    db.prepare('INSERT INTO spawns (map_id,entity_id,x,y,z,direction,respawn_seconds,group_size,enabled) VALUES (?,?,?,?,?,?,?,?,1)').bind(map.id,metin.id,612,384,0,0,600,1),
    db.prepare('INSERT INTO spawns (map_id,entity_id,x,y,z,direction,respawn_seconds,group_size,enabled) VALUES (?,?,?,?,?,?,?,?,1)').bind(map.id,npc.id,728,286,0,180,0,1),
    db.prepare('INSERT INTO drops (entity_id,item_id,chance,min_count,max_count,min_level,max_level) VALUES (?,?,?,?,?,?,?)').bind(captain.id,moonSword.id,1.2,1,1,30,55),
    db.prepare('INSERT INTO drops (entity_id,item_id,chance,min_count,max_count,min_level,max_level) VALUES (?,?,?,?,?,?,?)').bind(metin.id,moonSword.id,2.5,1,1,25,50),
    ...(shop ? [db.prepare('INSERT INTO shop_items (shop_id,item_id,buy_price,sell_price,position) VALUES (?,?,?,?,?)').bind(shop.id,sword.id,1500,250,0)] : []),
  ]);
  await migrateLegacyPlacements(db);
  await seedOriginalWorld(db);
}

type OriginalSpawnCatalog = {
  revision: string;
  maps: Array<{
    code: string; name: string; width: number; height: number;
    placements: Array<{
      sourceKey: string; targetKind: string; targetVnum: number;
      x: number; y: number; z: number; direction: number; spreadX: number; spreadY: number;
      respawnSeconds: number; percent: number; count: number;
    }>;
  }>;
};

async function seedOriginalWorld(db: D1Database) {
  const catalog = originalSpawnsJson as OriginalSpawnCatalog;
  const imported = await db.prepare("SELECT value FROM settings WHERE key='original_regens_revision'").first<{ value: string }>();
  if (imported?.value === catalog.revision) return;
  const now = new Date().toISOString();
  for (const map of catalog.maps) {
    await db.prepare(`INSERT INTO maps (code,name,width,height,enabled) VALUES (?,?,?,?,1)
      ON CONFLICT(code) DO UPDATE SET name=excluded.name,width=excluded.width,height=excluded.height,enabled=1`)
      .bind(map.code, map.name, map.width, map.height).run();
    const savedMap = await db.prepare('SELECT id FROM maps WHERE code=?').bind(map.code).first<{ id: number }>();
    if (!savedMap) continue;
    const statements = map.placements.map((placement) => db.prepare(`INSERT INTO world_placements
      (map_id,target_kind,target_vnum,x,y,z,direction,radius,spread_x,spread_y,spawn_percent,respawn_seconds,count,enabled,source_key,updated_at)
      VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,1,?,?)
      ON CONFLICT(source_key) DO UPDATE SET map_id=excluded.map_id,target_kind=excluded.target_kind,target_vnum=excluded.target_vnum,
      x=excluded.x,y=excluded.y,z=excluded.z,direction=excluded.direction,spread_x=excluded.spread_x,spread_y=excluded.spread_y,
      spawn_percent=excluded.spawn_percent,respawn_seconds=excluded.respawn_seconds,count=excluded.count,enabled=1,updated_at=excluded.updated_at`)
      .bind(savedMap.id, placement.targetKind, placement.targetVnum, placement.x, placement.y, placement.z,
        placement.direction, Math.max(placement.spreadX, placement.spreadY), placement.spreadX, placement.spreadY,
        placement.percent, placement.respawnSeconds, placement.count, placement.sourceKey, now));
    for (let offset = 0; offset < statements.length; offset += 50) await db.batch(statements.slice(offset, offset + 50));
  }
  await db.prepare(`INSERT INTO settings (key,value,updated_at) VALUES ('original_regens_revision',?,?)
    ON CONFLICT(key) DO UPDATE SET value=excluded.value,updated_at=excluded.updated_at`).bind(catalog.revision, now).run();
}

async function migrateLegacyPlacements(db: D1Database) {
  // Eski dünya editöründeki doğrulanmış koordinatları canlı oyun tablosuna
  // tek seferlik ve tekrar çalıştırılabilir biçimde taşır. Tahmini konum üretmez.
  try {
    const legacy = await db.prepare(`SELECT s.*,e.vnum AS target_vnum,e.type AS entity_type
      FROM spawns s JOIN entities e ON e.id=s.entity_id WHERE s.enabled=1`).all<Record<string, unknown>>();
    const current = await db.prepare('SELECT map_id,target_kind,target_vnum,x,y,z FROM world_placements').all<Record<string, unknown>>();
    const keys = new Set((current.results ?? []).map(placementKey));
    for (const row of legacy.results ?? []) {
      const kind = row.entity_type === 'npc' ? 'npc' : row.entity_type === 'metin' ? 'metin' : 'mob';
      const candidate = { ...row, target_kind: kind };
      const key = placementKey(candidate);
      if (keys.has(key)) continue;
      await db.prepare(`INSERT INTO world_placements
        (map_id,target_kind,target_vnum,x,y,z,direction,radius,respawn_seconds,count,enabled,updated_at)
        VALUES (?,?,?,?,?,?,?,?,?,?,1,?)`).bind(
          Number(row.map_id), kind, Number(row.target_vnum), Number(row.x), Number(row.y), Number(row.z ?? 0),
          Number(row.direction ?? 0), 0, kind === 'npc' ? 86400 : Math.max(1, Number(row.respawn_seconds ?? 60)),
          kind === 'npc' ? 1 : Math.max(1, Number(row.group_size ?? 1)), new Date().toISOString(),
        ).run();
      keys.add(key);
    }
  } catch (error) {
    // Eski tablo farklı bir sürümden gelmişse canlı API yine çalışmaya devam etsin.
    console.warn('[database] Eski yerleşimler taşınamadı:', error);
  }
}

function placementKey(row: Record<string, unknown>) {
  const coordinate = (value: unknown) => Number(value ?? 0).toFixed(3);
  return `${Number(row.map_id)}:${String(row.target_kind)}:${Number(row.target_vnum)}:${coordinate(row.x)}:${coordinate(row.y)}:${coordinate(row.z)}`;
}

async function seedBiology(db: D1Database) {
  const quests = [
    [30,'Biyoloğun Deneyi 1','Biyolog Chaegirab',30006,10,30220,'+%10 Hareket Hızı'],
    [40,'Biyoloğun Deneyi 2','Biyolog Chaegirab',30047,15,30221,'+%5 Saldırı Hızı'],
    [50,'Biyoloğun Deneyi 3','Biyolog Chaegirab',30015,15,30222,'+60 Defans'],
    [60,'Biyoloğun Deneyi 4','Biyolog Chaegirab',30050,20,30223,'+50 Saldırı Değeri'],
    [70,'Biyoloğun Deneyi 5','Biyolog Chaegirab',30165,25,30224,'+%11 Hareket Hızı ve +%10 Savunma'],
    [80,'Biyoloğun Deneyi 6','Biyolog Chaegirab',30166,30,30225,'+%6 Saldırı Hızı ve +%10 Yakın Dövüş Saldırısı'],
    [85,'Biyoloğun Deneyi 7','Biyolog Chaegirab',30167,40,30226,'Oyuncu sınıflarına karşı +%10 savunma'],
    [90,'Biyoloğun Deneyi 8','Biyolog Chaegirab',30168,50,30227,'Oyuncu sınıflarına karşı +%8 güçlü'],
    [92,'Seon-Pyeong Araştırması 1','Seon-Pyeong',30251,10,null,'Seçim: +1000 HP / +120 Defans / +51 Saldırı Değeri'],
    [94,'Seon-Pyeong Araştırması 2','Seon-Pyeong',30252,20,30228,'Seçim: +1100 HP / +140 Defans / +60 Saldırı Değeri'],
  ] as const;
  await db.prepare("DELETE FROM biology_levels WHERE level=30 AND item_vnum=100 AND reward='Biyolog görevi ödülü'").run();
  for (const quest of quests) await db.prepare(`INSERT OR IGNORE INTO biology_levels (level,quest_name,giver_name,item_vnum,item_count,soul_item_vnum,success_chance,cooldown_minutes,reward,enabled) VALUES (?,?,?,?,?,?,100,1440,?,1)`).bind(...quest).run();
  const rewards = [
    [30,0,'stat','MOVE_SPEED',10,null,1,'+%10 Hareket Hızı'],[30,0,'item','ITEM',1,50109,1,'Kırmızı Abanoz Sandık'],
    [40,0,'stat','ATTACK_SPEED',5,null,1,'+%5 Saldırı Hızı'],[40,0,'item','ITEM',1,50110,1,'İhtişamlı Abanoz Sandık'],
    [50,0,'stat','DEFENSE',60,null,1,'+60 Defans'],[50,0,'item','ITEM',1,50111,1,'Sarı Abanoz Sandık'],
    [60,0,'stat','ATTACK_VALUE',50,null,1,'+50 Saldırı Değeri'],[60,0,'item','ITEM',1,50112,1,'Parlak Yeşil Abanoz Sandık'],
    [70,0,'stat','MOVE_SPEED',11,null,1,'+%11 Hareket Hızı'],[70,0,'stat','DAMAGE_REDUCTION',10,null,1,'+%10 Savunma'],[70,0,'item','ITEM',1,50113,1,'Yeşil Abanoz Sandık'],
    [80,0,'stat','ATTACK_SPEED',6,null,1,'+%6 Saldırı Hızı'],[80,0,'stat','MELEE_ATTACK',10,null,1,'+%10 Yakın Dövüş Saldırısı'],[80,0,'item','ITEM',1,50114,1,'Mavi Abanoz Sandık'],
    [85,0,'stat','PVP_DEFENSE',10,null,1,'Sınıflara karşı +%10 savunma'],[85,0,'item','ITEM',1,50115,1,'Koyu Kırmızı Abanoz Sandık'],
    [90,0,'stat','PVP_DAMAGE',8,null,1,'Sınıflara karşı +%8 güçlü'],[90,0,'item','ITEM',1,50114,1,'Mavi Abanoz Sandık'],
    [92,1,'stat','MAX_HP',1000,null,1,'+1000 Maks. HP'],[92,1,'stat','DEFENSE',120,null,1,'+120 Defans'],[92,1,'stat','ATTACK_VALUE',51,null,1,'+51 Saldırı Değeri'],
    [94,1,'stat','MAX_HP',1100,null,1,'+1100 Maks. HP'],[94,1,'stat','DEFENSE',140,null,1,'+140 Defans'],[94,1,'stat','ATTACK_VALUE',60,null,1,'+60 Saldırı Değeri'],
  ] as const;
  const existing = await db.prepare('SELECT COUNT(*) AS count FROM biology_rewards').first<{count:number}>();
  if (Number(existing?.count ?? 0) === 0) for (const reward of rewards) await db.prepare('INSERT INTO biology_rewards (biology_level,choice_group,reward_type,reward_key,reward_value,item_vnum,item_count,label,enabled) VALUES (?,?,?,?,?,?,?,?,1)').bind(...reward).run();
}

export async function audit(actor: string, action: string, resource: string, resourceId: string | number | null, summary: string) {
  const now = new Date().toISOString();
  await database().prepare('INSERT INTO audit_logs (actor,action,resource,resource_id,summary,created_at) VALUES (?,?,?,?,?,?)')
    .bind(actor, action, resource, resourceId == null ? null : String(resourceId), summary, now).run();
}
