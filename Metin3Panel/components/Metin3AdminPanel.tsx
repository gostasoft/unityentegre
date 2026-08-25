'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Activity, AlertTriangle, Ban, CalendarDays, Check, ChevronDown, CircleGauge, Coins,
  Database, Edit3, Gem, LayoutDashboard, LoaderCircle, MapPinned, Menu, PackageOpen,
  Plus, RefreshCw, Save, Search, Settings2, ShieldCheck, ShoppingCart, Skull, Store,
  Swords, Trash2, Users, X,
} from 'lucide-react';

type Row = Record<string, any>;
type PanelData = {
  maps: Row[]; entities: Row[]; items: Row[]; spawns: Row[]; drops: Row[];
  shops: Row[]; shopItems: Row[]; players: Row[]; events: Row[]; settings: Row[]; logs: Row[];
};

const emptyData: PanelData = { maps: [], entities: [], items: [], spawns: [], drops: [], shops: [], shopItems: [], players: [], events: [], settings: [], logs: [] };

const nav = [
  ['dashboard','Kontrol Merkezi',LayoutDashboard], ['world','Dünya & Yerleşimler',MapPinned],
  ['entities','Mob & Metin Taşları',Skull], ['shops','NPC & Mağazalar',Store],
  ['items','İtem Yönetimi',PackageOpen], ['drops','Drop Sistemi',Gem],
  ['players','Oyuncular',Users], ['events','Etkinlik Takvimi',CalendarDays],
  ['settings','Sunucu Ayarları',Settings2],
] as const;

const fieldSets: Record<string, Array<[string,string,string,boolean?]>> = {
  maps: [['code','Harita kodu','text'],['name','Harita adı','text'],['width','Genişlik','number'],['height','Yükseklik','number'],['enabled','Aktif','checkbox']],
  entities: [['vnum','VNUM','number'],['name','Ad','text'],['type','Tür: mob / metin / npc','select'],['rank','Rütbe','text'],['level','Seviye','number'],['hp','HP','number'],['exp','EXP','number'],['min_damage','Min. hasar','number'],['max_damage','Maks. hasar','number'],['defense','Savunma','number'],['attack_speed','Saldırı hızı','number'],['move_speed','Hareket hızı','number'],['enabled','Aktif','checkbox']],
  items: [['vnum','VNUM','number'],['name','İtem adı','text'],['category','Kategori','text'],['buy_price','Alış fiyatı','number'],['sell_price','Satış fiyatı','number'],['stackable','Üst üste konabilir','checkbox'],['enabled','Aktif','checkbox']],
  spawns: [['map_id','Harita','map'],['entity_id','Mob / Metin / NPC','entity'],['x','X koordinatı','number'],['y','Y koordinatı','number'],['z','Z koordinatı','number'],['direction','Yön','number'],['respawn_seconds','Yeniden doğma (sn)','number'],['group_size','Grup adedi','number'],['enabled','Aktif','checkbox']],
  drops: [['entity_id','Düşüren mob / metin','dropEntity'],['item_id','Düşecek item','item'],['chance','Şans (%)','number'],['min_count','Min. adet','number'],['max_count','Maks. adet','number'],['min_level','Min. oyuncu seviyesi','number'],['max_level','Maks. oyuncu seviyesi','number']],
  shops: [['entity_id','NPC','npc'],['name','Mağaza adı','text'],['enabled','Aktif','checkbox']],
  shop_items: [['shop_id','Mağaza','shop'],['item_id','İtem','item'],['buy_price','Alış fiyatı','number'],['sell_price','Satış fiyatı','number'],['position','Sıra','number']],
  players: [['account','Hesap','text'],['character_name','Karakter adı','text'],['empire','İmparatorluk','text'],['character_class','Sınıf','text'],['level','Seviye','number'],['online','Çevrimiçi','checkbox']],
  events: [['name','Etkinlik adı','text'],['description','Açıklama','textarea'],['target_type','Hedef: all / mob / metin / npc','text'],['start_at','Başlangıç','datetime-local'],['end_at','Bitiş','datetime-local'],['multiplier','Çarpan','number'],['enabled','Aktif','checkbox']],
};

const titles: Record<string,string> = { maps:'Harita', entities:'Mob / Metin / NPC', items:'İtem', spawns:'Yerleşim', drops:'Drop', shops:'Mağaza', shop_items:'Mağaza İtemi', players:'Oyuncu', events:'Etkinlik' };

function fmt(value: unknown) { return Number(value ?? 0).toLocaleString('tr-TR'); }
function dateText(value: string | null) { return value ? new Date(value).toLocaleString('tr-TR',{dateStyle:'short',timeStyle:'short'}) : '—'; }

export function Metin3AdminPanel({ user }: { user: { name: string; email: string } }) {
  const [view,setView] = useState('dashboard');
  const [data,setData] = useState<PanelData>(emptyData);
  const [loading,setLoading] = useState(true);
  const [saving,setSaving] = useState(false);
  const [query,setQuery] = useState('');
  const [modal,setModal] = useState<{resource:string;data:Row}|null>(null);
  const [toast,setToast] = useState('');
  const [mobileOpen,setMobileOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/admin?resource=bootstrap',{cache:'no-store'});
      if (!response.ok) throw new Error('Panel verileri alınamadı.');
      setData(await response.json());
    } catch (error) { setToast(error instanceof Error ? error.message : 'Bağlantı hatası'); }
    finally { setLoading(false); }
  },[]);
  useEffect(() => { void load(); },[load]);
  useEffect(() => { if (!toast) return; const timer=setTimeout(()=>setToast(''),3000); return()=>clearTimeout(timer); },[toast]);

  async function mutate(resource:string,action:string,payload:Row) {
    setSaving(true);
    try {
      const response=await fetch('/api/admin',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({resource,action,data:payload})});
      const result=await response.json();
      if(!response.ok) throw new Error(result.error || 'İşlem başarısız.');
      setModal(null); setToast('Değişiklik oyuna gönderilmek üzere kaydedildi.'); await load();
    } catch(error){ setToast(error instanceof Error ? error.message : 'İşlem başarısız.'); }
    finally{ setSaving(false); }
  }

  function openNew(resource:string, preset:Row={}) {
    const defaults:Record<string,Row>={ maps:{width:1024,height:1024,enabled:true},entities:{type:'mob',rank:'Normal',level:1,hp:100,exp:0,min_damage:1,max_damage:2,defense:0,attack_speed:100,move_speed:100,enabled:true},items:{category:'Diğer',buy_price:0,sell_price:0,stackable:false,enabled:true},spawns:{map_id:data.maps[0]?.id,entity_id:data.entities[0]?.id,x:512,y:512,z:0,direction:0,respawn_seconds:60,group_size:1,enabled:true},drops:{entity_id:data.entities.find(x=>x.type!=='npc')?.id,item_id:data.items[0]?.id,chance:1,min_count:1,max_count:1,min_level:1,max_level:120},shops:{entity_id:data.entities.find(x=>x.type==='npc')?.id,enabled:true},shop_items:{shop_id:data.shops[0]?.id,item_id:data.items[0]?.id,buy_price:0,sell_price:0,position:0},players:{level:1,online:false},events:{target_type:'all',multiplier:1,enabled:true} };
    setModal({resource,data:{...(defaults[resource]||{}),...preset}});
  }

  const settings = useMemo(()=>Object.fromEntries(data.settings.map(row=>[row.key,row.value])),[data.settings]);
  const activeEvents=data.events.filter(row=>row.enabled && new Date(row.end_at)>new Date()).length;
  const onlinePlayers=data.players.filter(row=>row.online).length;
  const initials=user.name.split(/\s+/).map(part=>part[0]).join('').slice(0,2).toUpperCase();

  return <div className="app-shell">
    <aside className={`sidebar ${mobileOpen?'mobile-open':''}`}>
      <button className="sidebar-close" onClick={()=>setMobileOpen(false)} aria-label="Menüyü kapat"><X/></button>
      <div className="brand"><img src="/metin3-logo.png" alt="Metin 3"/><span>YÖNETİM PANELİ</span></div>
      <nav className="nav-list"><p className="nav-section">YÖNETİM</p>{nav.map(([id,label,Icon])=><button key={id} className={`nav-item ${view===id?'active':''}`} onClick={()=>{setView(id);setMobileOpen(false)}}><Icon size={18}/><span>{label}</span>{id==='players'&&<b>{onlinePlayers}</b>}</button>)}</nav>
      <div className="server-card"><div className="server-title"><Activity size={16}/> Oyun API Bağlantısı</div><div className="server-row"><span><i/> Panel Veritabanı</span><strong>Hazır</strong></div><div className="server-row"><span><i/> Unity Eşitleme</span><strong>60 sn</strong></div><div className="server-health"><span style={{width:'96%'}}/></div><small>Son veri yenileme: şimdi</small></div>
      <div className="admin-card"><div className="avatar">{initials}</div><div><strong>{user.name}</strong><span>{user.email}</span></div><ChevronDown size={16}/></div>
    </aside>
    {mobileOpen&&<button className="sidebar-scrim" aria-label="Menüyü kapat" onClick={()=>setMobileOpen(false)}/>}
    <main className="main">
      <header className="topbar"><button className="mobile-menu" onClick={()=>setMobileOpen(true)} aria-label="Menüyü aç"><Menu/></button><div className="search"><Search size={18}/><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Oyuncu, mob, item veya harita ara..."/><kbd>⌘ K</kbd></div><div className="top-actions"><div className="live-badge"><i/> CANLI</div><button className="icon-button" title="Verileri yenile" onClick={()=>void load()}><RefreshCw className={loading?'spin':''} size={18}/></button><div className="avatar small">{initials}</div></div></header>
      <div className="content">
        {loading && !data.maps.length ? <Loading/> : <ViewRouter view={view} data={data} query={query} settings={settings} activeEvents={activeEvents} onlinePlayers={onlinePlayers} openNew={openNew} setModal={setModal} mutate={mutate}/>} 
      </div>
    </main>
    {modal&&<EditorModal modal={modal} setModal={setModal} data={data} saving={saving} save={(resource,row)=>void mutate(resource,'upsert',row)}/>} 
    {toast&&<div className="toast"><Check size={16}/>{toast}</div>}
  </div>;
}

function ViewRouter(props:any){
  if(props.view==='dashboard') return <Dashboard {...props}/>;
  if(props.view==='world') return <WorldView {...props}/>;
  if(props.view==='entities') return <EntityView {...props}/>;
  if(props.view==='shops') return <ShopView {...props}/>;
  if(props.view==='items') return <ItemsView {...props}/>;
  if(props.view==='drops') return <DropsView {...props}/>;
  if(props.view==='players') return <PlayersView {...props}/>;
  if(props.view==='events') return <EventsView {...props}/>;
  return <SettingsView {...props}/>;
}

function Heading({eyebrow,title,description,action,label,icon:Icon=Plus}:{eyebrow:string;title:string;description:string;action?:()=>void;label?:string;icon?:any}){return <section className="page-heading"><div><p className="eyebrow"><CircleGauge size={14}/>{eyebrow}</p><h1>{title}</h1><p>{description}</p></div>{action&&<button className="primary" onClick={action}><Icon size={17}/>{label}</button>}</section>}
function Loading(){return <div className="loading"><LoaderCircle className="spin"/><strong>Oyun verileri hazırlanıyor</strong><span>Panel veritabanı güvenli biçimde açılıyor.</span></div>}

function Dashboard({data,activeEvents,onlinePlayers,openNew,setModal}:any){
 const stats=[['Çevrimiçi Oyuncu',onlinePlayers||0,'Canlı',Users,'green'],['Aktif Harita',data.maps.filter((x:Row)=>x.enabled).length,'Sorunsuz',MapPinned,'blue'],['Tanımlı İtem',data.items.length,'Kayıtlı',Coins,'gold'],['Aktif Etkinlik',activeEvents,'Planlı',Swords,'red']];
 return <><Heading eyebrow="GENEL BAKIŞ" title="Kontrol Merkezi" description="Metin 3 dünyasındaki tüm sistemlerin anlık durumu." action={()=>openNew('spawns')} label="Hızlı Yerleşim" icon={MapPinned}/><section className="stat-grid">{stats.map(([label,value,delta,Icon,color]:any)=><article className="stat-card" key={label}><div className={`stat-icon ${color}`}><Icon size={20}/></div><p>{label}</p><div><strong>{fmt(value)}</strong><span className={color}>{delta}</span></div><div className="sparkline"><i/><i/><i/><i/><i/><i/><i/></div></article>)}</section><section className="dashboard-grid"><WorldMap data={data} openNew={openNew}/><article className="panel activity-panel"><div className="panel-head"><div><span className="panel-kicker">SON İŞLEMLER</span><h2>Yönetici Akışı</h2></div></div><div className="activity-list">{data.logs.slice(0,6).map((log:Row)=><div className="activity-item" key={log.id}><div className={`activity-icon ${log.resource==='entities'?'mob':log.resource==='drops'?'drop':'map'}`}><Activity/></div><div><strong>{log.summary}</strong><p>{log.actor}</p><time>{dateText(log.created_at)}</time></div></div>)}</div></article></section><section className="quick-grid"><button onClick={()=>setModal({resource:'entities',data:data.entities[0]||{}})}><span className="red"><Skull/></span><div><strong>Mob Düzenle</strong><small>HP, EXP ve hasar değerleri</small></div><b>→</b></button><button onClick={()=>openNew('drops')}><span className="gold"><Gem/></span><div><strong>Drop Tablosu</strong><small>İtem ve yüzde oranları</small></div><b>→</b></button><button onClick={()=>openNew('events')}><span className="blue"><CalendarDays/></span><div><strong>Etkinlik Planla</strong><small>Takvim ve hedef gruplar</small></div><b>→</b></button></section></>;
}

function WorldMap({data,openNew}:any){const map=data.maps[0];return <article className="panel world-panel"><div className="panel-head"><div><span className="panel-kicker">DÜNYA YÖNETİMİ</span><h2>{map?.name||'Canlı Harita'}</h2></div><button className="secondary">Tüm haritalar <ChevronDown size={15}/></button></div><div className="world-map"><div className="map-grid"/><div className="map-land land-one"/><div className="map-land land-two"/><div className="map-land land-three"/>{data.spawns.slice(0,8).map((spawn:Row)=><button key={spawn.id} className={`map-pin ${spawn.entity_type==='npc'?'blue':spawn.entity_type==='metin'?'gold':'red'}`} style={{left:`${Math.min(90,Math.max(5,(spawn.x/(map?.width||1024))*100))}%`,top:`${Math.min(88,Math.max(8,(spawn.y/(map?.height||1024))*100))}%`}}><MapPinned size={13}/><span>{spawn.entity_name}</span></button>)}<div className="map-coordinates">X / Y koordinat sistemi</div></div><div className="world-footer"><div><span><i className="red-dot"/> {data.spawns.filter((x:Row)=>x.entity_type==='mob').length} Mob</span><span><i className="gold-dot"/> {data.spawns.filter((x:Row)=>x.entity_type==='metin').length} Metin</span><span><i className="blue-dot"/> {data.spawns.filter((x:Row)=>x.entity_type==='npc').length} NPC</span></div><button className="primary compact" onClick={()=>openNew('spawns')}><MapPinned size={16}/>Yerleşim Ekle</button></div></article>}

function WorldView({data,openNew,setModal,mutate}:any){return <><Heading eyebrow="DÜNYA DÜZENLEYİCİ" title="Haritalar & Yerleşimler" description="Mob, grup, metin taşı ve NPC'leri kesin koordinatlarla yerleştir." action={()=>openNew('spawns')} label="Yeni Yerleşim"/><section className="dashboard-grid world-editor"><WorldMap data={data} openNew={openNew}/><article className="panel"><div className="panel-head"><div><span className="panel-kicker">HARİTALAR</span><h2>{data.maps.length} harita</h2></div><button className="mini-add" onClick={()=>openNew('maps')}><Plus/></button></div><div className="compact-list">{data.maps.map((map:Row)=><button key={map.id} onClick={()=>setModal({resource:'maps',data:map})}><MapPinned/><span><strong>{map.name}</strong><small>{map.code} · {map.width}×{map.height}</small></span><Edit3/></button>)}</div></article></section><DataTable title="Yerleşim Listesi" rows={data.spawns} columns={[['entity_name','Varlık'],['entity_type','Tür'],['map_name','Harita'],['x','X'],['y','Y'],['respawn_seconds','Respawn']]} edit={row=>setModal({resource:'spawns',data:row})} remove={row=>mutate('spawns','delete',{id:row.id})}/></>}

function EntityView({data,query,openNew,setModal,mutate}:any){const rows=data.entities.filter((x:Row)=>(x.name+' '+x.vnum+' '+x.type).toLowerCase().includes(query.toLowerCase()));return <><Heading eyebrow="OYUN VARLIKLARI" title="Mob & Metin Taşları" description="Can, EXP, saldırı, savunma, hız ve rütbe değerlerini yönet." action={()=>openNew('entities')} label="Yeni Varlık"/><div className="filter-tabs"><span>Tümü <b>{rows.length}</b></span><span>Mob <b>{rows.filter((x:Row)=>x.type==='mob').length}</b></span><span>Metin <b>{rows.filter((x:Row)=>x.type==='metin').length}</b></span><span>NPC <b>{rows.filter((x:Row)=>x.type==='npc').length}</b></span></div><DataTable title="Varlık Kataloğu" rows={rows} columns={[['vnum','VNUM'],['name','Ad'],['type','Tür'],['level','Lv.'],['hp','HP'],['exp','EXP'],['min_damage','Min. Hasar'],['max_damage','Maks. Hasar'],['defense','Savunma']]} edit={row=>setModal({resource:'entities',data:row})} remove={row=>mutate('entities','delete',{id:row.id})}/></>}

function ItemsView({data,query,openNew,setModal,mutate}:any){const rows=data.items.filter((x:Row)=>(x.name+' '+x.vnum+' '+x.category).toLowerCase().includes(query.toLowerCase()));return <><Heading eyebrow="EKONOMİ" title="İtem Yönetimi" description="Yeni item ekle; alış, satış ve kategori değerlerini düzenle." action={()=>openNew('items')} label="Yeni İtem"/><DataTable title={`${rows.length} item`} rows={rows} columns={[['vnum','VNUM'],['name','İtem'],['category','Kategori'],['buy_price','Alış Fiyatı'],['sell_price','Satış Fiyatı'],['stackable','Yığın']]} edit={row=>setModal({resource:'items',data:row})} remove={row=>mutate('items','delete',{id:row.id})}/></>}

function DropsView({data,openNew,setModal,mutate}:any){return <><Heading eyebrow="ÖDÜL SİSTEMİ" title="Drop Tabloları" description="Her mob veya metin taşının düşüreceği itemleri yüzde şansıyla belirle." action={()=>openNew('drops')} label="Drop Ekle"/><div className="drop-summary"><div><Gem/><strong>{data.drops.length}</strong><span>Drop kuralı</span></div><div><Skull/><strong>{new Set(data.drops.map((x:Row)=>x.entity_id)).size}</strong><span>Kaynak varlık</span></div><div><PackageOpen/><strong>{new Set(data.drops.map((x:Row)=>x.item_id)).size}</strong><span>Farklı item</span></div></div><DataTable title="Drop Kuralları" rows={data.drops} columns={[['entity_name','Mob / Metin'],['item_name','İtem'],['chance','Şans %'],['min_count','Min.'],['max_count','Maks.'],['min_level','Min Lv.'],['max_level','Maks Lv.']]} edit={row=>setModal({resource:'drops',data:row})} remove={row=>mutate('drops','delete',{id:row.id})}/></>}

function ShopView({data,openNew,setModal,mutate}:any){return <><Heading eyebrow="NPC TİCARETİ" title="NPC & Mağazalar" description="NPC mağazalarındaki ürünleri ve özel alış/satış fiyatlarını yönet." action={()=>openNew('shops')} label="Mağaza Ekle"/><section className="split-cards"><article className="panel"><div className="panel-head"><h2>Mağazalar</h2><button className="mini-add" onClick={()=>openNew('shops')}><Plus/></button></div><div className="compact-list">{data.shops.map((shop:Row)=><button key={shop.id} onClick={()=>setModal({resource:'shops',data:shop})}><Store/><span><strong>{shop.name}</strong><small>NPC #{shop.entity_vnum} · {data.shopItems.filter((x:Row)=>x.shop_id===shop.id).length} ürün</small></span><Edit3/></button>)}</div></article><article className="panel"><div className="panel-head"><h2>Mağaza Ürünleri</h2><button className="mini-add" onClick={()=>openNew('shop_items')}><Plus/></button></div><div className="shop-items">{data.shopItems.map((item:Row)=><button key={item.id} onClick={()=>setModal({resource:'shop_items',data:item})}><ShoppingCart/><span><strong>{item.item_name}</strong><small>{item.shop_name}</small></span><b>{fmt(item.buy_price)} Yang</b></button>)}</div></article></section><DataTable title="Tüm Mağaza Ürünleri" rows={data.shopItems} columns={[['shop_name','Mağaza'],['item_name','İtem'],['buy_price','Alış'],['sell_price','Satış'],['position','Sıra']]} edit={row=>setModal({resource:'shop_items',data:row})} remove={row=>mutate('shop_items','delete',{id:row.id})}/></>}

function PlayersView({data,query,openNew,setModal,mutate}:any){const rows=data.players.filter((x:Row)=>(x.account+' '+x.character_name).toLowerCase().includes(query.toLowerCase()));function sanction(row:Row,type:string){const duration=type==='ban'?7*86400000:24*3600000;const until=new Date(Date.now()+duration).toISOString();void mutate('players',type,{id:row.id,until,reason:type==='ban'?'Yönetici kararı':'Sohbet kuralı ihlali'})}return <><Heading eyebrow="HESAP GÜVENLİĞİ" title="Oyuncu Yönetimi" description="Oyuncuları incele, banla, sustur veya yaptırımlarını kaldır." action={()=>openNew('players')} label="Oyuncu Kaydı"/><DataTable title={`${rows.filter((x:Row)=>x.online).length} çevrimiçi · ${rows.length} kayıt`} rows={rows} columns={[['character_name','Karakter'],['account','Hesap'],['empire','İmparatorluk'],['character_class','Sınıf'],['level','Lv.'],['online','Durum'],['ban_until','Ban Bitişi']]} edit={row=>setModal({resource:'players',data:row})} remove={row=>mutate('players','delete',{id:row.id})} extra={row=><><button title="7 gün banla" onClick={()=>sanction(row,'ban')}><Ban/></button><button title="24 saat sustur" onClick={()=>sanction(row,'mute')}><AlertTriangle/></button>{row.ban_until&&<button title="Cezayı kaldır" onClick={()=>mutate('players','unban',{id:row.id})}><ShieldCheck/></button>}</>}/></>}

function EventsView({data,openNew,setModal,mutate}:any){return <><Heading eyebrow="CANLI OPERASYON" title="Etkinlik Takvimi" description="Etkinlikleri zamanla, hedef grubunu seç ve oran çarpanını belirle." action={()=>openNew('events')} label="Etkinlik Planla"/><div className="event-grid">{data.events.map((event:Row)=><article className={`event-card ${event.enabled?'enabled':''}`} key={event.id}><div><CalendarDays/><span>{event.target_type.toUpperCase()}</span></div><h3>{event.name}</h3><p>{event.description||'Açıklama eklenmemiş.'}</p><dl><div><dt>Başlangıç</dt><dd>{dateText(event.start_at)}</dd></div><div><dt>Bitiş</dt><dd>{dateText(event.end_at)}</dd></div><div><dt>Çarpan</dt><dd>×{event.multiplier}</dd></div></dl><footer><button onClick={()=>setModal({resource:'events',data:event})}><Edit3/> Düzenle</button><button onClick={()=>mutate('events','delete',{id:event.id})}><Trash2/></button></footer></article>)}</div></>}

function SettingsView({settings,mutate}:any){const [form,setForm]=useState(settings);useEffect(()=>setForm(settings),[settings]);const groups=[['Deneyim Oranı','exp_rate','Oyuncuların kazandığı genel EXP çarpanı'],['Drop Oranı','drop_rate','Tüm item düşürme oranı'],['Yang Oranı','yang_rate','Kazanılan Yang çarpanı'],['Mob HP Oranı','mob_hp_rate','Tüm mob ve metin canı'],['Mob Hasar Oranı','mob_damage_rate','Tüm düşman saldırı hasarı']];return <><Heading eyebrow="GENEL YAPILANDIRMA" title="Sunucu Ayarları" description="Tüm oyun dünyasını etkileyen genel oranları tek yerden yönet."/><section className="settings-layout"><article className="panel settings-card"><div className="panel-head"><div><span className="panel-kicker">GLOBAL ORANLAR</span><h2>Oynanış Dengesi</h2></div></div>{groups.map(([label,key,description])=><label className="rate-row" key={key}><div><strong>{label}</strong><span>{description}</span></div><div><input type="range" min="0.1" max="10" step="0.1" value={form[key]||1} onChange={e=>setForm({...form,[key]:e.target.value})}/><b>×{Number(form[key]||1).toFixed(1)}</b></div></label>)}<div className="settings-actions"><button className="primary" onClick={()=>mutate('settings','save',form)}><Save/> Değişiklikleri Yayınla</button></div></article><article className="panel maintenance"><ShieldCheck/><h2>Bakım Modu</h2><p>Aktif edildiğinde yeni oyuncu girişleri durdurulur; yöneticiler oyunda kalabilir.</p><label className="switch"><input type="checkbox" checked={form.server_maintenance==='true'} onChange={e=>setForm({...form,server_maintenance:String(e.target.checked)})}/><span/></label><div className="warning"><AlertTriangle/> Bu ayar canlı oyuna anında gönderilir.</div></article></section></>}

function DataTable({title,rows,columns,edit,remove,extra}:any){return <article className="panel data-table"><div className="panel-head"><div><span className="panel-kicker">KAYITLAR</span><h2>{title}</h2></div></div><div className="table-scroll"><table><thead><tr>{columns.map((c:any)=><th key={c[0]}>{c[1]}</th>)}<th>İşlem</th></tr></thead><tbody>{rows.map((row:Row)=><tr key={row.id}>{columns.map(([key]:any)=><td key={key}>{typeof row[key]==='number'&&key!=='id'?fmt(row[key]):key.includes('until')?dateText(row[key]):row[key]===1?<span className="status good">Aktif</span>:row[key]===0?<span className="status">Pasif</span>:String(row[key]??'—')}</td>)}<td className="row-actions"><button onClick={()=>edit(row)} title="Düzenle"><Edit3/></button>{extra?.(row)}<button onClick={()=>{if(confirm('Bu kayıt silinsin mi?'))remove(row)}} title="Sil"><Trash2/></button></td></tr>)}</tbody></table>{!rows.length&&<div className="empty">Bu bölümde henüz kayıt yok.</div>}</div></article>}

function EditorModal({modal,setModal,data,saving,save}:any){const [form,setForm]=useState<Row>(modal.data);const fields=fieldSets[modal.resource]||[];function options(type:string){if(type==='map')return data.maps;if(type==='entity')return data.entities;if(type==='dropEntity')return data.entities.filter((x:Row)=>x.type!=='npc');if(type==='npc')return data.entities.filter((x:Row)=>x.type==='npc');if(type==='item')return data.items;if(type==='shop')return data.shops;return []}function optionText(type:string,row:Row){if(type==='map')return row.name;if(['entity','dropEntity','npc'].includes(type))return `#${row.vnum} ${row.name}`;if(type==='item')return `#${row.vnum} ${row.name}`;return row.name}return <div className="modal-backdrop" onMouseDown={e=>{if(e.target===e.currentTarget)setModal(null)}}><form className="editor-modal" onSubmit={e=>{e.preventDefault();save(modal.resource,form)}}><header><div><span>{form.id?'KAYDI DÜZENLE':'YENİ KAYIT'}</span><h2>{titles[modal.resource]||'Kayıt'}</h2></div><button type="button" onClick={()=>setModal(null)}><X/></button></header><div className="form-grid">{fields.map(([key,label,type])=><label className={type==='textarea'?'wide':''} key={key}>{type==='checkbox'?<><span>{label}</span><input type="checkbox" checked={Boolean(form[key])} onChange={e=>setForm({...form,[key]:e.target.checked})}/></>:<><span>{label}</span>{['map','entity','dropEntity','npc','item','shop'].includes(type)?<select value={form[key]??''} onChange={e=>setForm({...form,[key]:Number(e.target.value)})}>{options(type).map((row:Row)=><option key={row.id} value={row.id}>{optionText(type,row)}</option>)}</select>:type==='select'?<select value={form[key]||'mob'} onChange={e=>setForm({...form,[key]:e.target.value})}><option value="mob">Mob</option><option value="metin">Metin Taşı</option><option value="npc">NPC</option></select>:type==='textarea'?<textarea value={form[key]??''} onChange={e=>setForm({...form,[key]:e.target.value})}/>:<input required={['vnum','name','code','character_name'].includes(key)} type={type} step={type==='number'?'any':undefined} value={form[key]??''} onChange={e=>setForm({...form,[key]:type==='number'?Number(e.target.value):e.target.value})}/>}</>}</label>)}</div><footer><button type="button" className="secondary" onClick={()=>setModal(null)}>Vazgeç</button><button className="primary" disabled={saving}>{saving?<LoaderCircle className="spin"/>:<Save/>} Kaydet ve Yayınla</button></footer></form></div>}
