import { NextRequest, NextResponse } from 'next/server';
import { getChatGPTUser } from '../../../chatgpt-auth';
import { audit, database, ensureDatabase } from '../../../../lib/database';
import { findItem } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

const sanctionTypes = new Set(['account','character','hwid','pc','ip','mute']);

export async function GET(_: NextRequest, context: { params: Promise<{ id:string }> }) {
  const user = await getChatGPTUser();
  if (!user) return NextResponse.json({ error:'Yetkisiz erişim.' }, { status:401 });
  await ensureDatabase();
  const { id } = await context.params; const playerId = Number(id);
  if (!playerId) return NextResponse.json({ error:'Geçersiz oyuncu.' }, { status:400 });
  const db = database();
  const player = await db.prepare('SELECT * FROM players WHERE id=?').bind(playerId).first<Record<string, unknown>>();
  if (!player) return NextResponse.json({ error:'Oyuncu bulunamadı.' }, { status:404 });
  const [inventoryRows, locations, trades, sanctions, stationary] = await Promise.all([
    db.prepare('SELECT * FROM player_inventory WHERE player_id=? ORDER BY equipped DESC,slot').bind(playerId).all<Record<string, unknown>>(),
    db.prepare('SELECT * FROM player_location_history WHERE player_id=? ORDER BY entered_at DESC LIMIT 80').bind(playerId).all<Record<string, unknown>>(),
    db.prepare('SELECT * FROM trade_logs WHERE giver=? OR receiver=? ORDER BY created_at DESC LIMIT 30').bind(player.character_name, player.character_name).all<Record<string, unknown>>(),
    db.prepare('SELECT * FROM player_sanctions WHERE player_id=? ORDER BY active DESC,created_at DESC').bind(playerId).all<Record<string, unknown>>(),
    db.prepare('SELECT map_code,x,y,stationary_seconds,entered_at,left_at FROM player_location_history WHERE player_id=? ORDER BY stationary_seconds DESC LIMIT 1').bind(playerId).first<Record<string, unknown>>(),
  ]);
  const inventory = (inventoryRows.results ?? []).map((row) => ({ ...row, item_name:row.item_name || findItem(Number(row.item_vnum))?.name || `İtem #${row.item_vnum}` }));
  const tradeRows = trades.results ?? [];
  const tradeIds = tradeRows.map((row) => Number(row.id)).filter(Boolean);
  const tradeItems = tradeIds.length ? (await db.prepare(`SELECT * FROM trade_items WHERE trade_id IN (${tradeIds.map(()=>'?').join(',')}) ORDER BY trade_id DESC,id`).bind(...tradeIds).all<Record<string, unknown>>()).results : [];
  return NextResponse.json({ player, inventory, locations:locations.results ?? [], stationary:stationary ?? null, trades:tradeRows, tradeItems, sanctions:sanctions.results ?? [] });
}

export async function POST(request: NextRequest, context: { params: Promise<{ id:string }> }) {
  const user = await getChatGPTUser();
  if (!user) return NextResponse.json({ error:'Yetkisiz erişim.' }, { status:401 });
  await ensureDatabase();
  const { id } = await context.params; const playerId = Number(id);
  const body = await request.json() as { action?:string; sanctionType?:string; durationMinutes?:number|null; reason?:string; sanctionId?:number };
  const db = database();
  const player = await db.prepare('SELECT * FROM players WHERE id=?').bind(playerId).first<Record<string, unknown>>();
  if (!player) return NextResponse.json({ error:'Oyuncu bulunamadı.' }, { status:404 });

  if (body.action === 'revoke') {
    const sanctionId = Number(body.sanctionId ?? 0);
    const sanction = await db.prepare('SELECT * FROM player_sanctions WHERE id=? AND player_id=?').bind(sanctionId,playerId).first<Record<string, unknown>>();
    if (!sanction) return NextResponse.json({ error:'Yaptırım bulunamadı.' }, { status:404 });
    await db.prepare('UPDATE player_sanctions SET active=0 WHERE id=?').bind(sanctionId).run();
    if (sanction.sanction_type === 'mute') await db.prepare('UPDATE players SET mute_until=NULL WHERE id=?').bind(playerId).run();
    if (sanction.sanction_type === 'account' || sanction.sanction_type === 'character') await db.prepare('UPDATE players SET ban_until=NULL,ban_reason=NULL WHERE id=?').bind(playerId).run();
    await audit(user.email,'revoke','player_sanctions',sanctionId,`${player.character_name} yaptırımı kaldırıldı`);
    return NextResponse.json({ ok:true });
  }

  const type = String(body.sanctionType ?? '');
  if (body.action !== 'sanction' || !sanctionTypes.has(type)) return NextResponse.json({ error:'Geçersiz yaptırım türü.' }, { status:400 });
  const targets: Record<string,string> = { account:String(player.account??''), character:String(player.character_name??''), hwid:String(player.hwid??''), pc:String(player.pc_id??''), ip:String(player.last_ip??''), mute:String(player.character_name??'') };
  const target = targets[type];
  if (!target) return NextResponse.json({ error:`Bu oyuncu için ${type.toUpperCase()} bilgisi henüz oyun tarafından gönderilmedi.` }, { status:400 });
  const now = new Date();
  const duration = body.durationMinutes == null ? null : Math.max(1,Math.min(5256000,Number(body.durationMinutes)));
  const expiresAt = duration ? new Date(now.getTime()+duration*60000).toISOString() : null;
  const reason = String(body.reason ?? '').trim() || 'Yönetici kararı';
  const saved = await db.prepare('INSERT INTO player_sanctions (player_id,sanction_type,target_value,reason,starts_at,expires_at,active,created_at,created_by) VALUES (?,?,?,?,?,?,1,?,?) RETURNING id').bind(playerId,type,target,reason,now.toISOString(),expiresAt,now.toISOString(),user.email).first<{id:number}>();
  if (type === 'mute') await db.prepare('UPDATE players SET mute_until=? WHERE id=?').bind(expiresAt ?? '9999-12-31T23:59:59.000Z',playerId).run();
  if (type === 'account' || type === 'character') await db.prepare('UPDATE players SET ban_until=?,ban_reason=? WHERE id=?').bind(expiresAt ?? '9999-12-31T23:59:59.000Z',reason,playerId).run();
  await audit(user.email,'sanction','player_sanctions',saved?.id ?? null,`${player.character_name}: ${type} yaptırımı`);
  return NextResponse.json({ ok:true,id:saved?.id ?? 0 });
}
