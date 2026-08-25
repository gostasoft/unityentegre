'use client';

import { usePathname } from 'next/navigation';
import { Ban, Boxes, ChartNoAxesColumnIncreasing, CircleDollarSign, Dna, Fish, History, KeyRound, RadioTower, ShieldUser, ShoppingBag, UserRound, Waypoints } from 'lucide-react';

const links = [
  ['/panel/system/accounts','Hesaplar',KeyRound], ['/panel/system/characters','Karakterler',UserRound],
  ['/panel/system/gm','GM Yetkileri',ShieldUser], ['/panel/system/bans','Ban Yönetimi',Ban],
  ['/panel/system/warps','Işınlanma Noktaları',Waypoints], ['/panel/system/chests','Sandık İçerikleri',Boxes],
  ['/panel/system/exp','EXP Tablosu',ChartNoAxesColumnIncreasing], ['/panel/system/biology','Biyolog',Dna],
  ['/panel/system/fishing','Balıkçılık',Fish], ['/panel/system/markets','Çevrimdışı Pazar',ShoppingBag],
  ['/panel/system/trades','Ticaret Kayıtları',CircleDollarSign], ['/panel/system/server-status','Kanal Durumu',RadioTower],
  ['/panel/system/history','İşlem Geçmişi',History],
] as const;

export function PanelExtendedNav() {
  const path = usePathname();
  return <>
    <p className="nav-section">GELİŞMİŞ YÖNETİM</p>
    {links.map(([href,label,Icon]) => <a key={href} href={href} className={`nav-item ${path===href?'active':''}`}><Icon size={18}/><span>{label}</span></a>)}
  </>;
}
