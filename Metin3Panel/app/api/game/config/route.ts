import { env } from 'cloudflare:workers';
import { NextRequest, NextResponse } from 'next/server';
import { database, ensureDatabase } from '../../../../lib/database';
import { findGroup, findMob } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

async function rows(sql: string) { return (await database().prepare(sql).all()).results; }

export async function GET(request: NextRequest) {
  const expected = env.GAME_API_KEY;
  const supplied = request.headers.get('authorization')?.replace(/^Bearer\s+/i, '');
  const localPreview = request.headers.get('x-metin3-local') === '1' && request.nextUrl.hostname === 'localhost';
  if ((!expected || supplied !== expected) && !localPreview)
    return NextResponse.json({ error: 'Geçersiz oyun sunucusu anahtarı.' }, { status: 401 });

  await ensureDatabase();
  const [settingsRows, maps, entities, items, spawns, worldPlacements, drops, shops, shopItems, events, sanctions, warps, expLevels, biology, chests, chestItems, fishing, fishingEvents, revision] = await Promise.all([
    rows('SELECT key,value FROM settings'),
    rows('SELECT * FROM maps WHERE enabled=1'),
    rows('SELECT * FROM entities WHERE enabled=1'),
    rows('SELECT * FROM items WHERE enabled=1'),
    rows('SELECT * FROM spawns WHERE enabled=1'),
    rows(`SELECT p.*,m.code AS map_code,m.name AS map_name FROM world_placements p JOIN maps m ON m.id=p.map_id WHERE p.enabled=1 AND m.enabled=1`),
    rows('SELECT * FROM drops'),
    rows('SELECT * FROM shops WHERE enabled=1'),
    rows('SELECT * FROM shop_items'),
    rows(`SELECT * FROM events WHERE enabled=1 AND datetime(end_at)>=datetime('now')`),
    rows(`SELECT account,character_name,ban_until,ban_reason,mute_until FROM players WHERE ban_until IS NOT NULL OR mute_until IS NOT NULL`),
    rows(`SELECT w.*,c.name AS category_name FROM warp_entries w LEFT JOIN warp_categories c ON c.id=w.category_id WHERE w.enabled=1 AND COALESCE(c.enabled,1)=1 ORDER BY c.position,w.name`),
    rows('SELECT level,required_exp FROM exp_levels ORDER BY level'),
    rows('SELECT * FROM biology_levels WHERE enabled=1 ORDER BY level'),
    rows('SELECT * FROM chests WHERE enabled=1 ORDER BY vnum'),
    rows('SELECT * FROM chest_items ORDER BY chest_vnum,chance DESC'),
    rows('SELECT * FROM fishing_rates WHERE enabled=1 ORDER BY chance DESC'),
    rows(`SELECT * FROM fishing_event_items WHERE enabled=1 AND (start_at IS NULL OR datetime(start_at)<=datetime('now')) AND (end_at IS NULL OR datetime(end_at)>=datetime('now'))`),
    database().prepare(`SELECT
      (SELECT COALESCE(MAX(updated_at),'') FROM world_placements) || ':' ||
      (SELECT COALESCE(MAX(updated_at),'') FROM entities) || ':' ||
      (SELECT COALESCE(MAX(updated_at),'') FROM items) || ':' ||
      (SELECT COALESCE(MAX(updated_at),'') FROM proto_overrides) || ':' ||
      (SELECT COALESCE(MAX(updated_at),'') FROM settings) || ':' ||
      (SELECT COUNT(*) FROM world_placements) || ':' ||
      (SELECT COUNT(*) FROM warp_entries) || ':' ||
      (SELECT COUNT(*) FROM chest_items) || ':' ||
      (SELECT COALESCE(MAX(required_exp),0) FROM exp_levels) AS version`).first<{version:string}>(),
  ]);
  const settings = Object.fromEntries((settingsRows as Array<{key:string;value:string}>).map((row) => [row.key,row.value]));
  const overrides = new Map((entities as Array<Record<string, unknown>>).map((row) => [Number(row.vnum), row]));
  const requestedGroups = (worldPlacements as Array<Record<string, unknown>>).filter((row) => row.target_kind === 'group').map((row) => findGroup(Number(row.target_vnum))).filter(Boolean);
  const requestedVnums = new Set<number>();
  for (const placement of worldPlacements as Array<Record<string, unknown>>) if (placement.target_kind !== 'group') requestedVnums.add(Number(placement.target_vnum));
  for (const group of requestedGroups) for (const member of group!.members) requestedVnums.add(member);
  const runtimeEntities = [...requestedVnums].map((vnum) => {
    const proto = findMob(vnum);
    if (!proto) return null;
    const override = overrides.get(vnum);
    return {
      vnum, name: override?.name ?? proto.name, type: override?.type ?? proto.kind,
      rank: override?.rank ?? proto.rank, level: override?.level ?? proto.level,
      hp: override?.hp ?? proto.hp, exp: override?.exp ?? proto.exp,
      min_damage: override?.min_damage ?? proto.minDamage, max_damage: override?.max_damage ?? proto.maxDamage,
      defense: override?.defense ?? proto.defense, attack_speed: override?.attack_speed ?? proto.attackSpeed,
      move_speed: override?.move_speed ?? proto.moveSpeed, folder: proto.folder,
    };
  }).filter(Boolean);
  return NextResponse.json({ version: revision?.version ?? '0', settings, maps, entities, runtimeEntities, items, spawns, worldPlacements, groups: requestedGroups, drops, shops, shopItems, events, sanctions, warps, expLevels, biology, chests, chestItems, fishing, fishingEvents });
}
