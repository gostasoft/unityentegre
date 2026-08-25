import { NextRequest, NextResponse } from 'next/server';
import { getChatGPTUser } from '../../chatgpt-auth';
import { audit, database, ensureDatabase } from '../../../lib/database';
import { findItem } from '../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

type Config = { table: string; columns: string[]; booleans?: string[]; order?: string; readOnly?: boolean };

const resources: Record<string, Config> = {
  accounts: { table:'accounts', columns:['login','email','status','empire','created_at','last_login'], order:'id DESC' },
  characters: { table:'account_characters', columns:['account_id','name','job','level','empire','map_code','x','y','playtime','online','last_play'], booleans:['online'], order:'level DESC,name' },
  gm: { table:'gm_accounts', columns:['login','character_name','authority','contact_ip','enabled'], booleans:['enabled'], order:'authority,login' },
  bans: { table:'bans', columns:['target_type','target','reason','expires_at','active','created_at'], booleans:['active'], order:'active DESC,id DESC' },
  warp_categories: { table:'warp_categories', columns:['name','position','enabled'], booleans:['enabled'], order:'position,name' },
  warps: { table:'warp_entries', columns:['category_id','name','map_code','x','y','min_level','cost','enabled'], booleans:['enabled'], order:'category_id,name' },
  exp: { table:'exp_levels', columns:['level','required_exp'], order:'level' },
  biology: { table:'biology_levels', columns:['level','quest_name','giver_name','item_vnum','item_count','soul_item_vnum','success_chance','cooldown_minutes','reward','enabled'], booleans:['enabled'], order:'level' },
  biology_rewards: { table:'biology_rewards', columns:['biology_level','choice_group','reward_type','reward_key','reward_value','item_vnum','item_count','label','enabled'], booleans:['enabled'], order:'biology_level,id' },
  chests: { table:'chests', columns:['vnum','name','roll_count','enabled'], booleans:['enabled'], order:'vnum' },
  chest_items: { table:'chest_items', columns:['chest_vnum','item_vnum','item_name','count','chance'], order:'chest_vnum,chance DESC' },
  fishing: { table:'fishing_rates', columns:['fish_vnum','name','chance','min_length','max_length','enabled'], booleans:['enabled'], order:'chance DESC,name' },
  fishing_events: { table:'fishing_event_items', columns:['item_vnum','item_name','chance','start_at','end_at','enabled'], booleans:['enabled'], order:'id DESC' },
  markets: { table:'markets', columns:['owner','shop_name','map_code','x','y','created_at','expires_at','active'], booleans:['active'], order:'active DESC,id DESC' },
  market_items: { table:'market_items', columns:['market_id','item_vnum','item_name','count','price','sold'], booleans:['sold'], order:'market_id,id' },
  trades: { table:'trade_logs', columns:['giver','receiver','yang','created_at','ip_address'], order:'id DESC' },
  trade_items: { table:'trade_items', columns:['trade_id','direction','item_vnum','item_name','count'], order:'trade_id DESC,id' },
  channels: { table:'server_channels', columns:['name','host','port','status','players','updated_at'], order:'id' },
};

async function requireAdmin() { return getChatGPTUser(); }

export async function GET(request: NextRequest) {
  const user = await requireAdmin();
  if (!user) return NextResponse.json({ error:'Yetkisiz erişim.' }, { status:401 });
  await ensureDatabase();
  const resource = request.nextUrl.searchParams.get('resource') ?? '';
  if (resource === 'history') {
    const rows = (await database().prepare('SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 250').all()).results;
    return NextResponse.json({ rows });
  }
  const config = resources[resource];
  if (!config) return NextResponse.json({ error:'Geçersiz yönetim modülü.' }, { status:400 });
  const rows = (await database().prepare(`SELECT * FROM ${config.table} ORDER BY ${config.order ?? 'id DESC'} LIMIT 1000`).all<Record<string, unknown>>()).results;
  const enriched = rows.map((row) => {
    if (resource === 'biology') return { ...row, item_name:findItem(Number(row.item_vnum))?.name ?? 'Proto kaydında yok', soul_item_name:row.soul_item_vnum ? findItem(Number(row.soul_item_vnum))?.name ?? 'Proto kaydında yok' : 'Gerekmez' };
    if (resource === 'biology_rewards' && row.item_vnum) return { ...row, item_name:findItem(Number(row.item_vnum))?.name ?? 'Proto kaydında yok' };
    return row;
  });
  return NextResponse.json({ rows:enriched });
}

export async function POST(request: NextRequest) {
  const user = await requireAdmin();
  if (!user) return NextResponse.json({ error:'Yetkisiz erişim.' }, { status:401 });
  await ensureDatabase();
  const body = await request.json() as { resource?:string; action?:string; data?:Record<string, unknown> };
  const resource = body.resource ?? '';
  const action = body.action ?? 'upsert';
  const data = body.data ?? {};
  const config = resources[resource];
  if (!config || config.readOnly) return NextResponse.json({ error:'Bu kaynak düzenlenemez.' }, { status:400 });
  const db = database();
  const id = Number(data.id ?? 0);

  if (action === 'delete') {
    if (!id) return NextResponse.json({ error:'Silinecek kayıt seçilmedi.' }, { status:400 });
    await db.prepare(`DELETE FROM ${config.table} WHERE id=?`).bind(id).run();
    await audit(user.email, 'delete', resource, id, `${resource} kaydı silindi`);
    return NextResponse.json({ ok:true });
  }

  const normalized: Record<string, unknown> = { ...data };
  const now = new Date().toISOString();
  if (resource === 'accounts' && !normalized.created_at) normalized.created_at = now;
  if (resource === 'bans' && !normalized.created_at) normalized.created_at = now;
  if (resource === 'markets' && !normalized.created_at) normalized.created_at = now;
  if (resource === 'trades' && !normalized.created_at) normalized.created_at = now;
  if (resource === 'channels') normalized.updated_at = now;
  const columns = config.columns.filter((column) => normalized[column] !== undefined);
  if (!columns.length) return NextResponse.json({ error:'Kaydedilecek alan bulunamadı.' }, { status:400 });
  const values = columns.map((column) => config.booleans?.includes(column) ? (normalized[column] ? 1 : 0) : normalized[column]);
  try {
    let savedId = id;
    if (id) await db.prepare(`UPDATE ${config.table} SET ${columns.map((column) => `${column}=?`).join(',')} WHERE id=?`).bind(...values, id).run();
    else {
      const result = await db.prepare(`INSERT INTO ${config.table} (${columns.join(',')}) VALUES (${columns.map(() => '?').join(',')}) RETURNING id`).bind(...values).first<{id:number}>();
      savedId = result?.id ?? 0;
    }
    await audit(user.email, id ? 'update' : 'create', resource, savedId, String(data.name ?? data.login ?? data.target ?? `${resource} kaydı`));
    return NextResponse.json({ ok:true, id:savedId });
  } catch (reason) {
    const message = reason instanceof Error ? reason.message : 'Kayıt işlemi başarısız.';
    return NextResponse.json({ error:message.includes('UNIQUE') ? 'Bu kimlik veya ad zaten kayıtlı.' : 'Kayıt doğrulanamadı; zorunlu alanları kontrol edin.' }, { status:400 });
  }
}
