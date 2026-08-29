import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const sourceRoot = process.argv[2] ?? 'C:/Users/Salih Gökmen/Desktop/Seyir2 Bilgiler/Proto';
const groupPath = process.argv[3] ?? 'C:/Users/Salih Gökmen/Desktop/Seyir2 Bilgiler/Group Güncel/group.txt';
const itemDescriptionPath = process.argv[4] ?? 'C:/Metin3Web/itemdesc.txt';
const npcListPath = process.argv[5] ?? 'C:/Users/Salih Gökmen/Desktop/RootPackUnlocker/Source/npclist.txt';
const outputPath = fileURLToPath(new URL('../data/proto-catalog.json', import.meta.url));
const decoder = new TextDecoder('iso-8859-9');

function text(file) {
  return decoder.decode(fs.readFileSync(file));
}

function rows(file) {
  return text(file).split(/\r?\n/).filter(Boolean).map((line) => line.split('\t'));
}

function number(value, fallback = 0) {
  const parsed = Number(String(value ?? '').replace(',', '.'));
  return Number.isFinite(parsed) ? parsed : fallback;
}

function names(file) {
  const result = new Map();
  for (const row of rows(file).slice(1)) {
    const vnum = number(row[0], -1);
    if (vnum >= 0 && row[1]) result.set(vnum, row[1].trim());
  }
  return result;
}

const mobNames = names(path.join(sourceRoot, 'mob_names.txt'));
const npcFolders = new Map();
const npcFolderAliases = new Map();
const convertedKeys = new Set();
const convertedPack = path.join(process.env.USERPROFILE ?? '', 'Desktop', 'Metin 2 Mobil Dönüşüm Pack');
for (const relativeRoot of ['Monster/ymir work/monster', 'monster2/ymir work/monster2', 'NPC/ymir work/npc', 'npc2/ymir work/npc2']) {
  const root = path.join(convertedPack, relativeRoot);
  if (!fs.existsSync(root)) continue;
  for (const entry of fs.readdirSync(root, { recursive: true, withFileTypes: true })) {
    if (!entry.isFile() || !/\.(fbx|msm)$/i.test(entry.name)) continue;
    convertedKeys.add(path.basename(entry.name, path.extname(entry.name)).toLowerCase());
    convertedKeys.add(path.basename(entry.parentPath ?? entry.path ?? '').toLowerCase());
  }
}
if (fs.existsSync(npcListPath)) {
  for (const row of rows(npcListPath)) {
    const vnum = number(row[0], -1);
    const folder = String(row[1] ?? '').trim().replaceAll('\\', '/').replace(/\/$/, '');
    const sharedFolder = String(row[2] ?? '').trim().replaceAll('\\', '/').replace(/\/$/, '');
    if (vnum === 0 && folder && sharedFolder) npcFolderAliases.set(folder.split('/').at(-1), sharedFolder.split('/').at(-1));
    if (vnum > 0 && folder) npcFolders.set(vnum, folder.split('/').at(-1));
  }
}
for (const [vnum, folder] of npcFolders) {
  const key = folder.toLowerCase();
  npcFolders.set(vnum, convertedKeys.has(key) ? folder : (npcFolderAliases.get(folder) ?? folder));
}
const itemNames = names(path.join(sourceRoot, 'item_names.txt'));
const itemDescriptions = new Map();
if (fs.existsSync(itemDescriptionPath)) {
  for (const row of rows(itemDescriptionPath)) {
    const vnum = number(row[0], -1);
    if (vnum >= 0 && row[2]) itemDescriptions.set(vnum, row[2].trim());
  }
}
const mobRows = rows(path.join(sourceRoot, 'mob_proto.txt'));
const mobHeaders = mobRows[0].map((value) => value.trim());
const mobIndex = Object.fromEntries(mobHeaders.map((name, index) => [name, index]));

const mobs = mobRows.slice(1).flatMap((row) => {
  const vnum = number(row[mobIndex.Vnum], -1);
  const protoType = String(row[mobIndex.Type] ?? '').trim().toUpperCase();
  if (vnum < 0 || !['MONSTER', 'STONE', 'NPC', 'PET', 'WARP', 'GOTO', 'DOOR'].includes(protoType)) return [];
  return [{
    vnum,
    name: mobNames.get(vnum) ?? String(row[mobIndex.Name] ?? `VNUM ${vnum}`).trim(),
    kind: protoType === 'STONE' ? 'metin' : ['NPC', 'WARP', 'GOTO', 'DOOR'].includes(protoType) ? 'npc' : 'mob',
    protoType,
    rank: String(row[mobIndex.Rank] ?? 'PAWN').trim(),
    battleType: String(row[mobIndex.BattleType] ?? '').trim(),
    level: number(row[mobIndex.Level], 1),
    size: number(row[mobIndex.Size], 100),
    aiFlags: String(row[mobIndex.AiFlags] ?? '').trim(),
    raceFlags: String(row[mobIndex.RaceFlags] ?? '').trim(),
    immuneFlags: String(row[mobIndex.ImmuneFlags] ?? '').trim(),
    folder: npcFolders.get(vnum) ?? String(row[mobIndex.Folder] ?? '').trim(),
    st: number(row[mobIndex.St]), dx: number(row[mobIndex.Dx]), ht: number(row[mobIndex.Ht]), iq: number(row[mobIndex.Iq]),
    hp: number(row[mobIndex.MaxHp], 1),
    regenCycle: number(row[mobIndex.RegenCycle]),
    regenPercent: number(row[mobIndex.RegenPercent]),
    minGold: number(row[mobIndex.MinGold]),
    maxGold: number(row[mobIndex.MaxGold]),
    exp: number(row[mobIndex.Exp]),
    minDamage: number(row[mobIndex.MinDamage]),
    maxDamage: number(row[mobIndex.MaxDamage]),
    defense: number(row[mobIndex.Def]),
    attackSpeed: number(row[mobIndex.AttackSpeed], 100),
    moveSpeed: number(row[mobIndex.MoveSpeed], 100),
    aggressiveHpPct: number(row[mobIndex.AggressiveHpPct]),
    aggressiveSight: number(row[mobIndex.AggressiveSight]),
    attackRange: number(row[mobIndex.AttackRange]),
    dropItemGroup: number(row[mobIndex.DropItemGroup]),
  }];
});

const itemRows = rows(path.join(sourceRoot, 'item_proto.txt')).slice(1);
const itemProtoByVnum = new Map();
const itemRanges = [];
for (const row of itemRows) {
  const token = String(row[0] ?? '').trim();
  const match = token.match(/^(\d+)(?:~(\d+))?$/);
  if (!match) continue;
  const start = Number(match[1]);
  const end = match[2] ? Number(match[2]) : start;
  if (start === end) itemProtoByVnum.set(start, row);
  else itemRanges.push({ start, end, row });
}

for (const vnum of itemNames.keys()) {
  if (itemProtoByVnum.has(vnum)) continue;
  const range = itemRanges.find((candidate) => vnum >= candidate.start && vnum <= candidate.end);
  if (range) itemProtoByVnum.set(vnum, range.row);
}
const allItemVnums = new Set([...itemNames.keys(), ...itemProtoByVnum.keys()]);
const items = [...allItemVnums].sort((a, b) => a - b).map((vnum) => {
  const row = itemProtoByVnum.get(vnum) ?? [];
  return {
    vnum,
    name: itemNames.get(vnum) ?? String(row[1] ?? `VNUM ${vnum}`).trim(),
    description: itemDescriptions.get(vnum) ?? '',
    type: String(row[2] ?? 'ITEM_NONE').trim(),
    subtype: String(row[3] ?? '0').trim(),
    size: number(row[4], 1),
    antiFlags: String(row[5] ?? '').trim(),
    flags: String(row[6] ?? '').trim(),
    wearFlags: String(row[7] ?? '').trim(),
    immuneFlags: String(row[8] ?? '').trim(),
    gold: number(row[9]),
    shopBuyPrice: number(row[10]),
    refineVnum: number(row[11]),
    refineSet: number(row[12]),
    magicPct: number(row[13]),
    limitType0: String(row[14] ?? '').trim(),
    limitValue0: number(row[15]),
    limitType1: String(row[16] ?? '').trim(),
    limitValue1: number(row[17]),
    addonType0: String(row[18] ?? '').trim(), addonValue0: number(row[19]),
    addonType1: String(row[20] ?? '').trim(), addonValue1: number(row[21]),
    addonType2: String(row[22] ?? '').trim(), addonValue2: number(row[23]),
    value0: number(row[24]), value1: number(row[25]), value2: number(row[26]),
    value3: number(row[27]), value4: number(row[28]), value5: number(row[29]),
    specular: number(row[30]), socket: number(row[31]),
  };
});

const localizedMobName = new Map(mobs.map((mob) => [mob.vnum, mob.name]));
const groups = [];
const groupSource = text(groupPath);
for (const match of groupSource.matchAll(/Group\s+(?:"([^"]+)"|([^\r\n{]+))\s*\{([\s\S]*?)\}/g)) {
  const body = match[3];
  const vnum = number(body.match(/\bVnum\s+(\d+)/)?.[1], -1);
  if (vnum < 0) continue;
  const members = [];
  for (const member of body.matchAll(/^\s*(?:Leader|\d+)\s+(?:"[^"]+"|\S+)\s+(\d+)\s*$/gm)) {
    const memberVnum = Number(member[1]);
    if (!members.includes(memberVnum)) members.push(memberVnum);
  }
  const leaderVnum = number(body.match(/^\s*Leader\s+(?:"[^"]+"|\S+)\s+(\d+)\s*$/m)?.[1], members[0] ?? 0);
  const leaderName = localizedMobName.get(leaderVnum) ?? `VNUM ${leaderVnum}`;
  groups.push({ vnum, name: `${leaderName} Grubu`, leaderVnum, members });
}

const catalog = {
  generatedAt: new Date().toISOString(),
  source: {
    mobProto: path.join(sourceRoot, 'mob_proto.txt'),
    mobNames: path.join(sourceRoot, 'mob_names.txt'),
    itemProto: path.join(sourceRoot, 'item_proto.txt'),
    itemNames: path.join(sourceRoot, 'item_names.txt'),
    itemDescriptions: fs.existsSync(itemDescriptionPath) ? itemDescriptionPath : '',
    groups: groupPath,
    npcList: fs.existsSync(npcListPath) ? npcListPath : '',
    encoding: 'ISO-8859-9',
  },
  mobs,
  items,
  groups,
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, JSON.stringify(catalog));
console.log(`Proto catalog: ${mobs.length} mob/npc/metin, ${items.length} item, ${groups.length} group`);
