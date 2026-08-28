'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Boxes, ChevronLeft, ChevronRight, CircleGauge, Edit3, Gem, LayoutDashboard, LoaderCircle, MapPin, PackageOpen, RefreshCw, Search, Skull, Trash2, UsersRound, X } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { PanelExtendedNav } from './PanelExtendedNav';

type Kind = 'mobs' | 'metins' | 'items';
type Row = Record<string, any>;
type CatalogResponse = { rows: Row[]; page: number; pages: number; total: number; generatedAt: string };

const copy = {
  mobs: { title: 'Moblar', eyebrow: 'MOB_PROTO + MOB_NAMES', description: 'Tüm yaratık, patron, NPC ve canavar kayıtlarını gerçek proto değerleriyle yönet.', icon: Skull },
  metins: { title: 'Metinler', eyebrow: 'STONE PROTO KATALOĞU', description: 'Metin taşlarının seviye, HP, EXP, savunma ve hasar değerlerini yönet.', icon: Gem },
  items: { title: 'İtemler', eyebrow: 'ITEM_PROTO + ITEM_NAMES', description: 'Tüm itemleri tür, alt tür, fiyat, geliştirme ve kullanım sınırlarıyla yönet.', icon: PackageOpen },
};

export function ProtoCatalogPanel({ kind, user }: { kind: Kind; user: { name: string; email: string } }) {
  const router = useRouter();
  const [mode, setMode] = useState<'catalog' | 'groups'>(kind === 'mobs' ? 'catalog' : 'catalog');
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<CatalogResponse>({ rows: [], page: 1, pages: 1, total: 0, generatedAt: '' });
  const [loading, setLoading] = useState(true);
  const [placement, setPlacement] = useState<Row | null>(null);
  const [maps, setMaps] = useState<Row[]>([]);
  const [placements, setPlacements] = useState<Row[]>([]);
  const [toast, setToast] = useState('');
  const pageKind = mode === 'groups' ? 'groups' : kind;
  const ui = copy[kind];
  const initials = user.name.split(/\s+/).map((part) => part[0]).join('').slice(0, 2).toUpperCase();

  useEffect(() => { const timer = setTimeout(() => { setDebouncedQuery(query); setPage(1); }, 250); return () => clearTimeout(timer); }, [query]);
  useEffect(() => { if (!toast) return; const timer = setTimeout(() => setToast(''), 3200); return () => clearTimeout(timer); }, [toast]);
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const response = await fetch(`/api/catalog?kind=${pageKind}&page=${page}&query=${encodeURIComponent(debouncedQuery)}`, { cache: 'no-store' });
      if (!response.ok) throw new Error('Proto kataloğu alınamadı.');
      setData(await response.json());
    } catch (error) { setToast(error instanceof Error ? error.message : 'Bağlantı hatası'); }
    finally { setLoading(false); }
  }, [pageKind, page, debouncedQuery]);
  const loadPlacements = useCallback(async () => {
    const response = await fetch('/api/placements', { cache: 'no-store' });
    if (response.ok) { const result = await response.json(); setMaps(result.maps); setPlacements(result.placements); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  useEffect(() => { void loadPlacements(); }, [loadPlacements]);

  async function savePlacement(row: Row) {
    const response = await fetch('/api/placements', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'upsert', data: row }) });
    const result = await response.json();
    if (!response.ok) return setToast(result.error ?? 'Yerleşim kaydedilemedi.');
    setPlacement(null); setToast('Yerleşim yayınlandı; açık oyun oturumuna otomatik yansıyacak.'); await loadPlacements();
  }
  async function removePlacement(id: number) {
    if (!confirm('Bu canlı dünya yerleşimi silinsin mi?')) return;
    await fetch('/api/placements', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'delete', data: { id } }) });
    setToast('Yerleşim kaldırıldı.'); await loadPlacements();
  }
  function place(row: Row) {
    setPlacement({ map_id: maps[0]?.id, target_kind: mode === 'groups' ? 'group' : kind === 'metins' ? 'metin' : 'mob', target_vnum: row.vnum, target_name: row.name, x: 512, y: 512, z: 0, direction: 0, radius: 0, respawn_seconds: 60, count: 1, enabled: true });
  }
  function openDetail(row: Row) {
    router.push(`/panel/${mode === 'groups' ? 'groups' : kind}/${row.vnum}`);
  }
  const columns = useMemo(() => mode === 'groups'
    ? [['vnum','Grup VNUM'],['name','Grup'],['leaderVnum','Lider'],['memberCount','Üye']]
    : kind === 'items'
      ? [['vnum','VNUM'],['name','İtem'],['type','Tür'],['subtype','Alt Tür'],['buy_price','Alış'],['sell_price','Satış'],['source','Kaynak']]
      : [['vnum','VNUM'],['name','Ad'],['rank','Rütbe'],['level','Lv.'],['hp','HP'],['exp','EXP'],['min_damage','Min. Hasar'],['max_damage','Maks. Hasar'],['source','Kaynak']], [kind, mode]);

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><img src="/metin3-logo.png" alt="Metin 3"/><span>YÖNETİM PANELİ</span></div>
      <nav className="nav-list"><p className="nav-section">OYUN VERİLERİ</p>
        <a className="nav-item" href="/panel"><LayoutDashboard size={18}/><span>Kontrol Merkezi</span></a>
        <a className={`nav-item ${kind==='mobs'?'active':''}`} href="/panel/mobs"><Skull size={18}/><span>Moblar</span></a>
        <a className={`nav-item ${kind==='metins'?'active':''}`} href="/panel/metins"><Gem size={18}/><span>Metinler</span></a>
        <a className={`nav-item ${kind==='items'?'active':''}`} href="/panel/items"><PackageOpen size={18}/><span>İtemler</span></a>
        <PanelExtendedNav/>
      </nav>
      <div className="server-card"><div className="server-title"><CircleGauge size={16}/> Proto Doğrulaması</div><div className="server-row"><span><i/> mob_proto / names</span><strong>1.916</strong></div><div className="server-row"><span><i/> item_proto / names</span><strong>10.912</strong></div><div className="server-row"><span><i/> group.txt</span><strong>923</strong></div><small>Türkçe kodlama: ISO-8859-9 doğrulandı</small></div>
      <div className="admin-card"><div className="avatar">{initials}</div><div><strong>{user.name}</strong><span>{user.email}</span></div></div>
    </aside>
    <main className="main">
      <header className="topbar"><div className="search"><Search size={18}/><input value={query} onChange={(event)=>setQuery(event.target.value)} placeholder={`${ui.title} içinde ad veya VNUM ara...`}/><kbd>{data.total.toLocaleString('tr-TR')}</kbd></div><div className="top-actions"><div className="live-badge"><i/> OYUN SENKRONU</div><button className="icon-button" onClick={()=>void load()} title="Yenile"><RefreshCw className={loading?'spin':''} size={18}/></button><div className="avatar small">{initials}</div></div></header>
      <div className="content">
        <section className="page-heading"><div><p className="eyebrow"><ui.icon size={14}/>{ui.eyebrow}</p><h1>{mode==='groups'?'Mob Grupları':ui.title}</h1><p>{mode==='groups'?'group.txt içerisindeki lider ve üye VNUM’larını doğrulayarak haritaya grup yerleştir.':ui.description}</p></div></section>
        {kind==='mobs'&&<div className="catalog-tabs"><button className={mode==='catalog'?'active':''} onClick={()=>{setMode('catalog');setPage(1)}}><Skull/> Mob Kataloğu</button><button className={mode==='groups'?'active':''} onClick={()=>{setMode('groups');setPage(1)}}><UsersRound/> Mob Grupları</button></div>}
        <section className="catalog-summary"><div><ui.icon/><span><small>DOĞRULANAN KAYIT</small><b>{data.total.toLocaleString('tr-TR')}</b></span></div><div><MapPin/><span><small>CANLI YERLEŞİM</small><b>{placements.filter((row)=>mode==='groups'?row.target_kind==='group':row.target_kind===(kind==='metins'?'metin':'mob')).length}</b></span></div><div><Boxes/><span><small>PROTO TARİHİ</small><b>{data.generatedAt?new Date(data.generatedAt).toLocaleDateString('tr-TR'):'—'}</b></span></div></section>
        <article className="panel data-table catalog-table"><div className="panel-head"><div><span className="panel-kicker">PROTO KATALOĞU</span><h2>{data.total.toLocaleString('tr-TR')} kayıt</h2></div><div className="pagination"><button disabled={page<=1} onClick={()=>setPage(page-1)}><ChevronLeft/></button><span>{page} / {data.pages}</span><button disabled={page>=data.pages} onClick={()=>setPage(page+1)}><ChevronRight/></button></div></div>
          <div className="table-scroll"><table><thead><tr>{columns.map(([key,label])=><th key={key}>{label}</th>)}<th>İşlem</th></tr></thead><tbody>{data.rows.map((row)=><tr className="catalog-row" key={row.vnum} tabIndex={0} onClick={()=>openDetail(row)} onKeyDown={(event)=>{if(event.key==='Enter')openDetail(row)}}>{columns.map(([key])=><td key={key}>{typeof row[key]==='number'?Number(row[key]).toLocaleString('tr-TR'):String(row[key]??'—')}</td>)}<td className="row-actions"><button title="Detay sayfasını aç" onClick={(event)=>{event.stopPropagation();openDetail(row)}}><ChevronRight/></button>{kind!=='items'&&<button title="Haritaya yerleştir" onClick={(event)=>{event.stopPropagation();place(row)}}><MapPin/></button>}</td></tr>)}</tbody></table>{loading&&<div className="table-loading"><LoaderCircle className="spin"/> Veriler doğrulanıyor</div>}</div>
        </article>
        {kind!=='items'&&<article className="panel data-table"><div className="panel-head"><div><span className="panel-kicker">CANLI DÜNYA</span><h2>Son yerleşimler</h2></div></div><div className="table-scroll"><table><thead><tr><th>Hedef</th><th>Tür</th><th>Harita</th><th>X</th><th>Y</th><th>Z</th><th>Adet</th><th>İşlem</th></tr></thead><tbody>{placements.slice(0,12).map((row)=><tr key={row.id}><td>#{row.target_vnum} {row.target_name}</td><td>{row.target_kind}</td><td>{row.map_name}</td><td>{row.x}</td><td>{row.y}</td><td>{row.z}</td><td>{row.count}</td><td className="row-actions"><button onClick={()=>setPlacement(row)}><Edit3/></button><button onClick={()=>void removePlacement(row.id)}><Trash2/></button></td></tr>)}</tbody></table></div></article>}
      </div>
    </main>
    {placement&&<PlacementEditor row={placement} maps={maps} close={()=>setPlacement(null)} save={savePlacement}/>}
    {toast&&<div className="toast">{toast}</div>}
  </div>;
}

function PlacementEditor({row,maps,close,save}:{row:Row;maps:Row[];close:()=>void;save:(row:Row)=>void}) {
  const [form,setForm]=useState<Row>(row);
  const numeric=[['x','X koordinatı'],['y','Y koordinatı'],['z','Z yüksekliği'],['direction','Yön (derece)'],['radius','Dağılım yarıçapı'],['respawn_seconds','Yeniden doğma (sn)'],['count','Adet']];
  return <div className="modal-backdrop"><form className="editor-modal" onSubmit={(event)=>{event.preventDefault();save(form)}}><header><div><span>CANLI DÜNYA YERLEŞİMİ</span><h2>#{form.target_vnum} {form.target_name}</h2></div><button type="button" onClick={close}><X/></button></header><div className="form-grid"><label className="wide"><span>Harita</span><select value={form.map_id??''} onChange={(event)=>setForm({...form,map_id:Number(event.target.value)})}>{maps.map((map)=><option key={map.id} value={map.id}>{map.name} · {map.code}</option>)}</select></label>{numeric.map(([key,label])=><label key={key}><span>{label}</span><input type="number" step="any" value={form[key]??0} onChange={(event)=>setForm({...form,[key]:Number(event.target.value)})}/></label>)}</div><footer><button type="button" className="secondary" onClick={close}>Vazgeç</button><button className="primary"><MapPin/> Haritaya Yerleştir</button></footer></form></div>;
}
