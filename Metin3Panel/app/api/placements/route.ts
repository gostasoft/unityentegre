import { NextRequest, NextResponse } from 'next/server';
import { getChatGPTUser } from '../../chatgpt-auth';
import { audit, database, ensureDatabase } from '../../../lib/database';
import { findGroup, findMob } from '../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

async function listPlacements() {
  const result = await database().prepare(`SELECT p.*,m.name AS map_name,m.code AS map_code
    FROM world_placements p JOIN maps m ON m.id=p.map_id ORDER BY p.updated_at DESC,p.id DESC`).all();
  return result.results.map((row) => {
    const kind = String(row.target_kind);
    const vnum = Number(row.target_vnum);
    const target = kind === 'group' ? findGroup(vnum) : findMob(vnum);
    return { ...row, target_name: target?.name ?? `VNUM ${vnum}` };
  });
}

export async function GET() {
  if (!await getChatGPTUser()) return NextResponse.json({ error: 'Yetkisiz erişim.' }, { status: 401 });
  await ensureDatabase();
  const [maps, placements] = await Promise.all([
    database().prepare('SELECT * FROM maps WHERE enabled=1 ORDER BY name').all(),
    listPlacements(),
  ]);
  return NextResponse.json({ maps: maps.results, placements });
}

export async function POST(request: NextRequest) {
  const user = await getChatGPTUser();
  if (!user) return NextResponse.json({ error: 'Yetkisiz erişim.' }, { status: 401 });
  await ensureDatabase();
  const body = await request.json() as { action?: string; data?: Record<string, unknown> };
  const data = body.data ?? {};
  const id = Number(data.id ?? 0);
  if (body.action === 'delete') {
    if (!id) return NextResponse.json({ error: 'Yerleşim seçilmedi.' }, { status: 400 });
    await database().prepare('DELETE FROM world_placements WHERE id=?').bind(id).run();
    await audit(user.email, 'delete', 'world_placements', id, 'Canlı dünya yerleşimi silindi');
    return NextResponse.json({ ok: true });
  }
  const kind = String(data.target_kind ?? 'mob');
  const targetVnum = Number(data.target_vnum);
  const mapId = Number(data.map_id);
  if (!['mob', 'metin', 'group'].includes(kind) || !targetVnum || !mapId)
    return NextResponse.json({ error: 'Harita ve hedef VNUM zorunludur.' }, { status: 400 });
  if (kind === 'group' ? !findGroup(targetVnum) : !findMob(targetVnum))
    return NextResponse.json({ error: 'VNUM proto dosyalarında doğrulanamadı.' }, { status: 400 });
  const values = [mapId, kind, targetVnum, Number(data.x ?? 0), Number(data.y ?? 0), Number(data.z ?? 0), Number(data.direction ?? 0), Number(data.radius ?? 0), Math.max(1, Number(data.respawn_seconds ?? 60)), Math.max(1, Number(data.count ?? 1)), data.enabled === false ? 0 : 1, new Date().toISOString()];
  let savedId = id;
  if (id) {
    await database().prepare(`UPDATE world_placements SET map_id=?,target_kind=?,target_vnum=?,x=?,y=?,z=?,direction=?,radius=?,respawn_seconds=?,count=?,enabled=?,updated_at=? WHERE id=?`).bind(...values, id).run();
  } else {
    const result = await database().prepare(`INSERT INTO world_placements (map_id,target_kind,target_vnum,x,y,z,direction,radius,respawn_seconds,count,enabled,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?) RETURNING id`).bind(...values).first<{id:number}>();
    savedId = result?.id ?? 0;
  }
  await audit(user.email, id ? 'update' : 'create', 'world_placements', savedId, `${kind} #${targetVnum} canlı dünyaya yerleştirildi`);
  return NextResponse.json({ ok: true, id: savedId });
}
