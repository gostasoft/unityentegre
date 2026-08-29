import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const sourceRoot = process.argv[2] ?? 'C:/Users/Salih Gökmen/Desktop/Seyir2 Bilgiler/Kırmızı 1.Köy';
const protoPath = fileURLToPath(new URL('../data/proto-catalog.json', import.meta.url));
const outputPath = fileURLToPath(new URL('../data/original-spawns.json', import.meta.url));
const proto = JSON.parse(fs.readFileSync(protoPath, 'utf8'));
const mobs = new Map(proto.mobs.map((row) => [Number(row.vnum), row]));
const groups = new Map(proto.groups.map((row) => [Number(row.vnum), row]));

function value(text, key, fallback = 0) {
  const match = text.match(new RegExp(`^\\s*${key}\\s+([0-9.]+)(?:\\s+([0-9.]+))?`, 'mi'));
  if (!match) return Array.isArray(fallback) ? fallback : Number(fallback);
  return match[2] == null ? Number(match[1]) : [Number(match[1]), Number(match[2])];
}

function seconds(text) {
  const match = String(text).trim().match(/^(\d+(?:\.\d+)?)([smhd]?)$/i);
  if (!match) throw new Error(`Geçersiz yenilenme süresi: ${text}`);
  return Math.max(1, Math.round(Number(match[1]) * ({ s: 1, m: 60, h: 3600, d: 86400 }[match[2].toLowerCase()] ?? 1)));
}

function groupVnum(raw) {
  if (groups.has(raw)) return raw;
  if (groups.has(raw + 100)) return raw + 100;
  throw new Error(`Regen grup VNUM'u group.txt içinde bulunamadı: ${raw}`);
}

function importMap(mapDirectory) {
  const mapCode = path.basename(mapDirectory);
  const regenPath = path.join(mapDirectory, 'regen.txt');
  const settingPath = path.join(mapDirectory, 'setting.txt');
  if (!fs.existsSync(regenPath) || !fs.existsSync(settingPath)) return null;
  const setting = fs.readFileSync(settingPath, 'utf8');
  const cellScale = value(setting, 'CellScale', 200);
  const mapSize = value(setting, 'MapSize', [1, 1]);
  const unityScale = cellScale / 100;
  const placements = [];
  const lines = fs.readFileSync(regenPath, 'utf8').split(/\r?\n/);
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index].trim();
    if (!line || line.startsWith('//')) continue;
    const columns = line.split(/\s+/);
    if (columns.length < 11 || !['g', 'm'].includes(columns[0])) continue;
    const [sourceType, cx, cy, sx, sy, z, direction, respawn, percent, count, rawVnum] = columns;
    const raw = Number(rawVnum);
    const targetKind = sourceType === 'g' ? 'group' : mobs.get(raw)?.kind;
    const targetVnum = sourceType === 'g' ? groupVnum(raw) : raw;
    if (!targetKind || (targetKind !== 'group' && !mobs.has(targetVnum)))
      throw new Error(`${mapCode}:${index + 1} VNUM proto içinde bulunamadı: ${raw}`);
    placements.push({
      sourceKey: `original:${mapCode}:regen:${index + 1}`,
      sourceType,
      mapCode,
      targetKind,
      targetVnum,
      x: Number(cx) * unityScale,
      y: Number(cy) * unityScale,
      z: Number(z) * unityScale,
      direction: Number(direction) === 0 ? 0 : (Number(direction) - 1) * 45,
      spreadX: Number(sx) * unityScale,
      spreadY: Number(sy) * unityScale,
      respawnSeconds: targetKind === 'npc' ? Math.max(86400, seconds(respawn)) : seconds(respawn),
      percent: Number(percent),
      count: Math.max(1, Number(count)),
    });
  }
  return {
    code: mapCode,
    name: mapCode === 'metin2_map_c1' ? 'Jinno Birinci Köy' : mapCode,
    width: mapSize[0] * 256 * unityScale,
    height: mapSize[1] * 256 * unityScale,
    source: regenPath,
    placements,
  };
}

const maps = fs.readdirSync(sourceRoot, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => importMap(path.join(sourceRoot, entry.name)))
  .filter(Boolean);
if (!maps.length) throw new Error(`Regen içeren harita bulunamadı: ${sourceRoot}`);
const instanceCount = maps.flatMap((map) => map.placements).reduce((sum, placement) => {
  if (placement.targetKind !== 'group') return sum + placement.count;
  return sum + placement.count * (groups.get(placement.targetVnum)?.members.length ?? 0);
}, 0);
const revision = crypto.createHash('sha256').update(JSON.stringify(maps)).digest('hex').slice(0, 16);
fs.writeFileSync(outputPath, JSON.stringify({ generatedAt: new Date().toISOString(), revision, maps }, null, 2));
console.log(`Original regens: ${maps.length} map, ${maps.flatMap((map) => map.placements).length} area, ${instanceCount} entity instance`);
