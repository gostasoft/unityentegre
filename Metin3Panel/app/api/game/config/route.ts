import { env } from 'cloudflare:workers';
import { NextRequest, NextResponse } from 'next/server';
import { database, ensureDatabase } from '../../../../lib/database';

export const dynamic = 'force-dynamic';

async function rows(sql: string) { return (await database().prepare(sql).all()).results; }

export async function GET(request: NextRequest) {
  const expected = env.GAME_API_KEY;
  const supplied = request.headers.get('authorization')?.replace(/^Bearer\s+/i, '');
  const localPreview = request.headers.get('x-metin3-local') === '1' && request.nextUrl.hostname === 'localhost';
  if ((!expected || supplied !== expected) && !localPreview)
    return NextResponse.json({ error: 'Geçersiz oyun sunucusu anahtarı.' }, { status: 401 });

  await ensureDatabase();
  const [settingsRows, maps, entities, items, spawns, drops, shops, shopItems, events, sanctions] = await Promise.all([
    rows('SELECT key,value FROM settings'),
    rows('SELECT * FROM maps WHERE enabled=1'),
    rows('SELECT * FROM entities WHERE enabled=1'),
    rows('SELECT * FROM items WHERE enabled=1'),
    rows('SELECT * FROM spawns WHERE enabled=1'),
    rows('SELECT * FROM drops'),
    rows('SELECT * FROM shops WHERE enabled=1'),
    rows('SELECT * FROM shop_items'),
    rows(`SELECT * FROM events WHERE enabled=1 AND datetime(end_at)>=datetime('now')`),
    rows(`SELECT account,character_name,ban_until,ban_reason,mute_until FROM players WHERE ban_until IS NOT NULL OR mute_until IS NOT NULL`),
  ]);
  const settings = Object.fromEntries((settingsRows as Array<{key:string;value:string}>).map((row) => [row.key,row.value]));
  return NextResponse.json({ version: new Date().toISOString(), settings, maps, entities, items, spawns, drops, shops, shopItems, events, sanctions });
}
