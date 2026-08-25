import { NextRequest, NextResponse } from 'next/server';
import { getChatGPTUser } from '../../chatgpt-auth';
import { audit, database, ensureDatabase } from '../../../lib/database';
import { itemCategory, protoCatalog, type ProtoItem, type ProtoMob } from '../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';
const PAGE_SIZE = 80;

async function requireAdmin() { return getChatGPTUser(); }

function normalized(value: unknown) {
  return String(value ?? '').toLocaleLowerCase('tr-TR').trim();
}

export async function GET(request: NextRequest) {
  const user = await requireAdmin();
  if (!user) return NextResponse.json({ error: 'Yetkisiz erişim.' }, { status: 401 });
  await ensureDatabase();
  const kind = request.nextUrl.searchParams.get('kind') ?? 'mobs';
  const query = normalized(request.nextUrl.searchParams.get('query'));
  const page = Math.max(1, Number(request.nextUrl.searchParams.get('page') ?? 1));
  let source: Array<ProtoMob | ProtoItem | { vnum: number; name: string; leaderVnum: number; members: number[] }>;
  if (kind === 'mobs') source = protoCatalog.mobs.filter((row) => row.kind !== 'metin');
  else if (kind === 'metins') source = protoCatalog.mobs.filter((row) => row.kind === 'metin');
  else if (kind === 'items') source = protoCatalog.items;
  else if (kind === 'groups') source = protoCatalog.groups;
  else return NextResponse.json({ error: 'Geçersiz katalog.' }, { status: 400 });

  const filtered = query
    ? source.filter((row) => normalized(`${row.vnum} ${row.name} ${'folder' in row ? row.folder : ''} ${'type' in row ? row.type : ''}`).includes(query))
    : source;
  const start = (page - 1) * PAGE_SIZE;
  const pageRows = filtered.slice(start, start + PAGE_SIZE);
  const vnums = pageRows.map((row) => row.vnum);
  let overrides: Array<Record<string, unknown>> = [];
  if (vnums.length && kind !== 'groups') {
    const placeholders = vnums.map(() => '?').join(',');
    const table = kind === 'items' ? 'items' : 'entities';
    overrides = (await database().prepare(`SELECT * FROM ${table} WHERE vnum IN (${placeholders})`).bind(...vnums).all()).results;
  }
  const overrideByVnum = new Map(overrides.map((row) => [Number(row.vnum), row]));
  const rows = pageRows.map((row) => {
    const override = overrideByVnum.get(row.vnum);
    if (kind === 'items') {
      const item = row as ProtoItem;
      return {
        ...item, category: itemCategory(item.type), buy_price: item.shopBuyPrice,
        sell_price: item.gold, stackable: item.flags.includes('ITEM_STACKABLE') ? 1 : 0,
        ...override, proto_name: item.name, source: override ? 'Düzenlendi' : 'Proto',
      };
    }
    if (kind === 'groups') return { ...row, memberCount: 'members' in row ? row.members.length : 0, source: 'group.txt' };
    const mob = row as ProtoMob;
    return {
      ...mob, min_damage: mob.minDamage, max_damage: mob.maxDamage,
      attack_speed: mob.attackSpeed, move_speed: mob.moveSpeed,
      ...override, proto_name: mob.name, source: override ? 'Düzenlendi' : 'Proto',
    };
  });
  return NextResponse.json({
    kind, rows, page, pageSize: PAGE_SIZE, total: filtered.length,
    pages: Math.max(1, Math.ceil(filtered.length / PAGE_SIZE)),
    generatedAt: protoCatalog.generatedAt,
  });
}

export async function POST(request: NextRequest) {
  const user = await requireAdmin();
  if (!user) return NextResponse.json({ error: 'Yetkisiz erişim.' }, { status: 401 });
  await ensureDatabase();
  const body = await request.json() as { kind?: string; data?: Record<string, unknown> };
  const kind = body.kind ?? '';
  const data = body.data ?? {};
  const vnum = Number(data.vnum);
  if (!vnum) return NextResponse.json({ error: 'Geçerli bir VNUM gerekli.' }, { status: 400 });
  const now = new Date().toISOString();
  const db = database();
  if (kind === 'items') {
    await db.prepare(`INSERT INTO items (vnum,name,category,buy_price,sell_price,stackable,enabled,updated_at)
      VALUES (?,?,?,?,?,?,?,?) ON CONFLICT(vnum) DO UPDATE SET name=excluded.name,category=excluded.category,buy_price=excluded.buy_price,sell_price=excluded.sell_price,stackable=excluded.stackable,enabled=excluded.enabled,updated_at=excluded.updated_at`)
      .bind(vnum, String(data.name ?? ''), String(data.category ?? 'Diğer'), Number(data.buy_price ?? 0), Number(data.sell_price ?? 0), data.stackable ? 1 : 0, data.enabled === false ? 0 : 1, now).run();
  } else if (kind === 'mobs' || kind === 'metins') {
    const protoType = protoCatalog.mobs.find((row) => row.vnum === vnum)?.kind;
    const type = kind === 'metins' ? 'metin' : protoType ?? 'mob';
    await db.prepare(`INSERT INTO entities (vnum,name,type,rank,level,hp,exp,min_damage,max_damage,defense,attack_speed,move_speed,enabled,updated_at)
      VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(vnum) DO UPDATE SET name=excluded.name,type=excluded.type,rank=excluded.rank,level=excluded.level,hp=excluded.hp,exp=excluded.exp,min_damage=excluded.min_damage,max_damage=excluded.max_damage,defense=excluded.defense,attack_speed=excluded.attack_speed,move_speed=excluded.move_speed,enabled=excluded.enabled,updated_at=excluded.updated_at`)
      .bind(vnum, String(data.name ?? ''), type, String(data.rank ?? 'PAWN'), Number(data.level ?? 1), Number(data.hp ?? 1), Number(data.exp ?? 0), Number(data.min_damage ?? 0), Number(data.max_damage ?? 0), Number(data.defense ?? 0), Number(data.attack_speed ?? 100), Number(data.move_speed ?? 100), data.enabled === false ? 0 : 1, now).run();
  } else return NextResponse.json({ error: 'Geçersiz katalog türü.' }, { status: 400 });
  await audit(user.email, 'proto_update', kind, vnum, `#${vnum} ${String(data.name ?? '')} güncellendi`);
  return NextResponse.json({ ok: true });
}
