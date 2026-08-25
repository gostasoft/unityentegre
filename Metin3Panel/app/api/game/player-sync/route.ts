import { env } from 'cloudflare:workers';
import { NextRequest, NextResponse } from 'next/server';
import { database, ensureDatabase } from '../../../../lib/database';
import { findItem } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

type InventoryItem = { slot:number; itemVnum:number; itemName?:string; count?:number; equipped?:boolean; sockets?:unknown[]; attributes?:unknown[] };
type SyncBody = { account?:string; characterName?:string; empire?:string; characterClass?:string; level?:number; online?:boolean; yang?:number; won?:number; mapCode?:string; x?:number; y?:number; hwid?:string; pcId?:string; inventory?:InventoryItem[] };

function authorized(request:NextRequest){const expected=env.GAME_API_KEY;const supplied=request.headers.get('authorization')?.replace(/^Bearer\s+/i,'');return Boolean(expected&&supplied===expected)||(request.headers.get('x-metin3-local')==='1'&&request.nextUrl.hostname==='localhost')}

export async function POST(request:NextRequest){
  if(!authorized(request))return NextResponse.json({error:'Geçersiz oyun sunucusu anahtarı.'},{status:401});
  await ensureDatabase();const body=await request.json() as SyncBody;
  const characterName=String(body.characterName??'').trim();const account=String(body.account??'').trim();
  if(!characterName||!account)return NextResponse.json({error:'account ve characterName zorunludur.'},{status:400});
  const db=database();const now=new Date();const nowIso=now.toISOString();
  const previous=await db.prepare('SELECT * FROM players WHERE character_name=?').bind(characterName).first<Record<string,unknown>>();
  const ip=request.headers.get('cf-connecting-ip')??request.headers.get('x-forwarded-for')?.split(',')[0]?.trim()??'';
  const player=await db.prepare(`INSERT INTO players (account,character_name,empire,character_class,level,online,last_seen,yang,won,last_map_code,last_x,last_y,last_ip,hwid,pc_id) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(character_name) DO UPDATE SET account=excluded.account,empire=excluded.empire,character_class=excluded.character_class,level=excluded.level,online=excluded.online,last_seen=excluded.last_seen,yang=excluded.yang,won=excluded.won,last_map_code=excluded.last_map_code,last_x=excluded.last_x,last_y=excluded.last_y,last_ip=CASE WHEN excluded.last_ip='' THEN players.last_ip ELSE excluded.last_ip END,hwid=CASE WHEN excluded.hwid='' THEN players.hwid ELSE excluded.hwid END,pc_id=CASE WHEN excluded.pc_id='' THEN players.pc_id ELSE excluded.pc_id END RETURNING id`).bind(account,characterName,String(body.empire??previous?.empire??'Shinsoo'),String(body.characterClass??previous?.character_class??'Savaşçı'),Math.max(1,Number(body.level??previous?.level??1)),body.online===false?0:1,nowIso,Math.max(0,Math.floor(Number(body.yang??previous?.yang??0))),Math.max(0,Math.floor(Number(body.won??previous?.won??0))),String(body.mapCode??previous?.last_map_code??''),Number(body.x??previous?.last_x??0),Number(body.y??previous?.last_y??0),ip,String(body.hwid??''),String(body.pcId??'')).first<{id:number}>();
  if(!player?.id)return NextResponse.json({error:'Oyuncu senkronu kaydedilemedi.'},{status:500});
  const mapCode=String(body.mapCode??'');
  if(mapCode){
    const open=await db.prepare('SELECT * FROM player_location_history WHERE player_id=? AND left_at IS NULL ORDER BY id DESC LIMIT 1').bind(player.id).first<Record<string,unknown>>();
    const previousSeen=previous?.last_seen?new Date(String(previous.last_seen)).getTime():now.getTime();const delta=Math.max(0,Math.min(300,Math.floor((now.getTime()-previousSeen)/1000)));
    const x=Number(body.x??0),y=Number(body.y??0);
    if(open&&open.map_code===mapCode){const distance=Math.hypot(x-Number(open.x??0),y-Number(open.y??0));await db.prepare('UPDATE player_location_history SET x=?,y=?,duration_seconds=duration_seconds+?,stationary_seconds=stationary_seconds+? WHERE id=?').bind(x,y,delta,distance<3?delta:0,open.id).run()}
    else{if(open)await db.prepare('UPDATE player_location_history SET left_at=? WHERE id=?').bind(nowIso,open.id).run();await db.prepare('INSERT INTO player_location_history (player_id,map_code,x,y,entered_at,duration_seconds,stationary_seconds) VALUES (?,?,?,?,?,0,0)').bind(player.id,mapCode,x,y,nowIso).run()}
  }
  if(Array.isArray(body.inventory)){
    await db.prepare('DELETE FROM player_inventory WHERE player_id=?').bind(player.id).run();
    for(const item of body.inventory.slice(0,500)){const vnum=Math.max(0,Math.floor(Number(item.itemVnum)));if(!vnum)continue;await db.prepare('INSERT INTO player_inventory (player_id,slot,item_vnum,item_name,count,equipped,sockets,attributes,updated_at) VALUES (?,?,?,?,?,?,?,?,?)').bind(player.id,Math.max(0,Math.floor(Number(item.slot))),vnum,String(item.itemName??findItem(vnum)?.name??''),Math.max(1,Math.floor(Number(item.count??1))),item.equipped?1:0,JSON.stringify(item.sockets??[]),JSON.stringify(item.attributes??[]),nowIso).run()}
  }
  return NextResponse.json({ok:true,playerId:player.id,syncedAt:nowIso});
}
