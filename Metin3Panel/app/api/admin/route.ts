import { NextRequest, NextResponse } from 'next/server';
import { getChatGPTUser } from '../../chatgpt-auth';
import { audit, database, ensureDatabase } from '../../../lib/database';

export const dynamic = 'force-dynamic';

const resourceConfig: Record<string, { table: string; columns: string[] }> = {
  maps: { table: 'maps', columns: ['code','name','width','height','enabled'] },
  entities: { table: 'entities', columns: ['vnum','name','type','rank','level','hp','exp','min_damage','max_damage','defense','attack_speed','move_speed','enabled'] },
  items: { table: 'items', columns: ['vnum','name','category','buy_price','sell_price','stackable','enabled'] },
  spawns: { table: 'spawns', columns: ['map_id','entity_id','x','y','z','direction','respawn_seconds','group_size','enabled'] },
  drops: { table: 'drops', columns: ['entity_id','item_id','chance','min_count','max_count','min_level','max_level'] },
  shops: { table: 'shops', columns: ['entity_id','name','enabled'] },
  shop_items: { table: 'shop_items', columns: ['shop_id','item_id','buy_price','sell_price','position'] },
  players: { table: 'players', columns: ['account','character_name','empire','character_class','level','online','last_seen','ban_until','ban_reason','mute_until'] },
  events: { table: 'events', columns: ['name','description','target_type','start_at','end_at','multiplier','enabled'] },
};

async function requireAdmin() {
  const user = await getChatGPTUser();
  if (!user) return null;
  return user;
}

async function all(sql: string) {
  return (await database().prepare(sql).all()).results;
}

export async function GET(request: NextRequest) {
  const user = await requireAdmin();
  if (!user) return NextResponse.json({ error: 'Yetkisiz erişim.' }, { status: 401 });
  await ensureDatabase();
  const resource = request.nextUrl.searchParams.get('resource') ?? 'bootstrap';
  if (resource !== 'bootstrap' && !resourceConfig[resource] && resource !== 'settings' && resource !== 'audit')
    return NextResponse.json({ error: 'Geçersiz kaynak.' }, { status: 400 });

  if (resource === 'bootstrap') {
    const [maps, entities, items, spawns, drops, shops, shopItems, players, events, settings, logs] = await Promise.all([
      all('SELECT * FROM maps ORDER BY name'),
      all('SELECT * FROM entities ORDER BY type, level DESC, name'),
      all('SELECT * FROM items ORDER BY category, name'),
      all(`SELECT s.*, m.name AS map_name, m.code AS map_code, e.name AS entity_name, e.vnum AS entity_vnum, e.type AS entity_type FROM spawns s JOIN maps m ON m.id=s.map_id JOIN entities e ON e.id=s.entity_id ORDER BY m.name,e.type,e.name`),
      all(`SELECT d.*, e.name AS entity_name, e.vnum AS entity_vnum, i.name AS item_name, i.vnum AS item_vnum FROM drops d JOIN entities e ON e.id=d.entity_id JOIN items i ON i.id=d.item_id ORDER BY e.name,d.chance DESC`),
      all(`SELECT s.*, e.name AS entity_name, e.vnum AS entity_vnum FROM shops s JOIN entities e ON e.id=s.entity_id ORDER BY s.name`),
      all(`SELECT si.*, s.name AS shop_name, i.name AS item_name, i.vnum AS item_vnum FROM shop_items si JOIN shops s ON s.id=si.shop_id JOIN items i ON i.id=si.item_id ORDER BY s.name,si.position`),
      all('SELECT * FROM players ORDER BY online DESC, level DESC, character_name'),
      all('SELECT * FROM events ORDER BY start_at DESC'),
      all('SELECT * FROM settings ORDER BY key'),
      all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 30'),
    ]);
    return NextResponse.json({ maps, entities, items, spawns, drops, shops, shopItems, players, events, settings, logs, user });
  }
  if (resource === 'settings') return NextResponse.json(await all('SELECT * FROM settings ORDER BY key'));
  if (resource === 'audit') return NextResponse.json(await all('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 100'));
  return NextResponse.json(await all(`SELECT * FROM ${resourceConfig[resource].table} ORDER BY id DESC`));
}

export async function POST(request: NextRequest) {
  const user = await requireAdmin();
  if (!user) return NextResponse.json({ error: 'Yetkisiz erişim.' }, { status: 401 });
  await ensureDatabase();
  const body = await request.json() as { resource?: string; action?: string; data?: Record<string, unknown> };
  const resource = body.resource ?? '';
  const action = body.action ?? 'upsert';
  const data = body.data ?? {};
  const db = database();

  if (resource === 'settings' && action === 'save') {
    const entries = Object.entries(data);
    if (!entries.length) return NextResponse.json({ ok: true });
    const now = new Date().toISOString();
    await db.batch(entries.map(([key, value]) => db.prepare('INSERT INTO settings (key,value,updated_at) VALUES (?,?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value,updated_at=excluded.updated_at').bind(key, String(value), now)));
    await audit(user.email, 'update', 'settings', null, `${entries.length} sunucu ayarı güncellendi`);
    return NextResponse.json({ ok: true });
  }

  if (resource === 'players' && (action === 'ban' || action === 'mute' || action === 'unban')) {
    const id = Number(data.id);
    if (!id) return NextResponse.json({ error: 'Oyuncu seçilmedi.' }, { status: 400 });
    if (action === 'unban') {
      await db.prepare('UPDATE players SET ban_until=NULL,ban_reason=NULL,mute_until=NULL WHERE id=?').bind(id).run();
      await audit(user.email, 'unban', 'players', id, 'Oyuncunun yaptırımları kaldırıldı');
    } else if (action === 'ban') {
      await db.prepare('UPDATE players SET ban_until=?,ban_reason=? WHERE id=?').bind(data.until ?? null, data.reason ?? '', id).run();
      await audit(user.email, 'ban', 'players', id, String(data.reason ?? 'Sebep belirtilmedi'));
    } else {
      await db.prepare('UPDATE players SET mute_until=? WHERE id=?').bind(data.until ?? null, id).run();
      await audit(user.email, 'mute', 'players', id, 'Sohbet susturması uygulandı');
    }
    return NextResponse.json({ ok: true });
  }

  const config = resourceConfig[resource];
  if (!config) return NextResponse.json({ error: 'Geçersiz kaynak.' }, { status: 400 });
  if (action === 'delete') {
    const id = Number(data.id);
    if (!id) return NextResponse.json({ error: 'Kayıt seçilmedi.' }, { status: 400 });
    await db.prepare(`DELETE FROM ${config.table} WHERE id=?`).bind(id).run();
    await audit(user.email, 'delete', resource, id, `${resource} kaydı silindi`);
    return NextResponse.json({ ok: true });
  }

  const now = new Date().toISOString();
  const normalized = { ...data };
  if (resource === 'entities' || resource === 'items') normalized.updated_at = now;
  if (resource === 'players' && !normalized.last_seen) normalized.last_seen = now;
  const columns = [...config.columns];
  if ((resource === 'entities' || resource === 'items') && !columns.includes('updated_at')) columns.push('updated_at');
  const supplied = columns.filter((column) => normalized[column] !== undefined);
  if (!supplied.length) return NextResponse.json({ error: 'Kaydedilecek alan bulunamadı.' }, { status: 400 });
  const values = supplied.map((column) => typeof normalized[column] === 'boolean' ? (normalized[column] ? 1 : 0) : normalized[column]);
  const id = Number(data.id ?? 0);
  let savedId = id;
  if (id) {
    await db.prepare(`UPDATE ${config.table} SET ${supplied.map((column) => `${column}=?`).join(',')} WHERE id=?`).bind(...values, id).run();
  } else {
    const result = await db.prepare(`INSERT INTO ${config.table} (${supplied.join(',')}) VALUES (${supplied.map(() => '?').join(',')}) RETURNING id`).bind(...values).first<{id:number}>();
    savedId = result?.id ?? 0;
  }
  await audit(user.email, id ? 'update' : 'create', resource, savedId, String(data.name ?? data.character_name ?? `${resource} kaydı`));
  return NextResponse.json({ ok: true, id: savedId });
}
