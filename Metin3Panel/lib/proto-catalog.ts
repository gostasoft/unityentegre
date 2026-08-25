import catalogJson from '../data/proto-catalog.json';

export type ProtoMob = {
  vnum: number; name: string; kind: 'mob' | 'metin' | 'npc'; protoType: string;
  rank: string; battleType: string; level: number; size: number; folder: string;
  aiFlags: string; raceFlags: string; immuneFlags: string; st: number; dx: number; ht: number; iq: number;
  hp: number; exp: number; minDamage: number; maxDamage: number; defense: number;
  regenCycle: number; regenPercent: number; minGold: number; maxGold: number; aggressiveHpPct: number;
  attackSpeed: number; moveSpeed: number; aggressiveSight: number; attackRange: number;
  dropItemGroup: number;
};

export type ProtoItem = {
  vnum: number; name: string; description: string; type: string; subtype: string; size: number;
  antiFlags: string; flags: string; wearFlags: string; immuneFlags: string; gold: number;
  shopBuyPrice: number; refineVnum: number; refineSet: number;
  limitType0: string; limitValue0: number; limitType1: string; limitValue1: number;
  magicPct: number; addonType0: string; addonValue0: number; addonType1: string; addonValue1: number;
  addonType2: string; addonValue2: number; value0: number; value1: number; value2: number;
  value3: number; value4: number; value5: number; specular: number; socket: number;
};

export type ProtoGroup = { vnum: number; name: string; leaderVnum: number; members: number[] };
type ProtoCatalog = {
  generatedAt: string;
  source: Record<string, string>;
  mobs: ProtoMob[];
  items: ProtoItem[];
  groups: ProtoGroup[];
};

export const protoCatalog = catalogJson as ProtoCatalog;

export function itemCategory(type: string) {
  const categories: Record<string, string> = {
    ITEM_WEAPON: 'Silah', ITEM_ARMOR: 'Zırh', ITEM_USE: 'Kullanılabilir', ITEM_AUTOUSE: 'Otomatik',
    ITEM_MATERIAL: 'Malzeme', ITEM_SPECIAL: 'Özel', ITEM_TOOL: 'Araç', ITEM_LOTTERY: 'Piyango',
    ITEM_ELK: 'Yang', ITEM_METIN: 'Taş', ITEM_CONTAINER: 'Sandık', ITEM_FISH: 'Balık',
    ITEM_ROD: 'Olta', ITEM_RESOURCE: 'Kaynak', ITEM_COSTUME: 'Kostüm', ITEM_DS: 'Ejderha Taşı',
    ITEM_RING: 'Yüzük', ITEM_BELT: 'Kemer', ITEM_PET: 'Pet',
  };
  return categories[type] ?? type.replace(/^ITEM_/, '').replaceAll('_', ' ');
}

export function findMob(vnum: number) { return protoCatalog.mobs.find((row) => row.vnum === vnum); }
export function findItem(vnum: number) { return protoCatalog.items.find((row) => row.vnum === vnum); }
export function findGroup(vnum: number) { return protoCatalog.groups.find((row) => row.vnum === vnum); }
