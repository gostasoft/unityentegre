'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ArrowLeft, CheckCircle2, CircleGauge, Edit3, Gem, LayoutDashboard, LoaderCircle, PackageOpen, Save, Shield, Skull, Sparkles, Swords, UsersRound, X } from 'lucide-react';
import { PanelExtendedNav } from './PanelExtendedNav';

type Kind = 'mobs' | 'metins' | 'items' | 'groups';
type Row = Record<string, any>;
type Field = [key: string, label: string, type?: 'number' | 'text' | 'flags'];

const pageCopy = {
  mobs: { title: 'Mob Detayı', list: 'Moblar', icon: Skull, accent: 'red' },
  metins: { title: 'Metin Detayı', list: 'Metinler', icon: Gem, accent: 'gold' },
  items: { title: 'İtem Detayı', list: 'İtemler', icon: PackageOpen, accent: 'blue' },
  groups: { title: 'Grup Detayı', list: 'Mob Grupları', icon: UsersRound, accent: 'green' },
};

export function CatalogDetailPage({ kind, vnum, user }: { kind: Kind; vnum: number; user: { name: string; email: string } }) {
  const [row, setRow] = useState<Row | null>(null);
  const [form, setForm] = useState<Row>({});
  const [editing, setEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [toast, setToast] = useState('');
  const ui = pageCopy[kind];
  const Icon = ui.icon;
  const initials = user.name.split(/\s+/).map((part) => part[0]).join('').slice(0, 2).toUpperCase();

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const response = await fetch(`/api/catalog?kind=${kind}&vnum=${vnum}`, { cache: 'no-store' });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error ?? 'Kayıt alınamadı.');
      if (!result.rows?.[0]) throw new Error('Bu VNUM proto kataloğunda bulunamadı.');
      setRow(result.rows[0]); setForm(result.rows[0]);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Bağlantı hatası.'); }
    finally { setLoading(false); }
  }, [kind, vnum]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => { if (!toast) return; const timer = setTimeout(() => setToast(''), 3200); return () => clearTimeout(timer); }, [toast]);

  const sections = useMemo(() => detailSections(kind, row ?? {}), [kind, row]);
  const editable = useMemo(() => editFields(kind), [kind]);

  async function save() {
    setSaving(true);
    try {
      const response = await fetch('/api/catalog', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ kind, data: form }) });
      const result = await response.json();
      if (!response.ok) throw new Error(result.error ?? 'Kayıt başarısız.');
      await load(); setEditing(false); setToast('Değişiklik kaydedildi ve oyuna yayınlandı.');
    } catch (reason) { setToast(reason instanceof Error ? reason.message : 'Kayıt başarısız.'); }
    finally { setSaving(false); }
  }

  return <div className="app-shell detail-shell">
    <aside className="sidebar">
      <div className="brand"><img src="/metin3-logo.png" alt="Metin 3"/><span>YÖNETİM PANELİ</span></div>
      <nav className="nav-list"><p className="nav-section">OYUN VERİLERİ</p>
        <a className="nav-item" href="/panel"><LayoutDashboard size={18}/><span>Kontrol Merkezi</span></a>
        <a className={`nav-item ${kind==='mobs'||kind==='groups'?'active':''}`} href="/panel/mobs"><Skull size={18}/><span>Moblar</span></a>
        <a className={`nav-item ${kind==='metins'?'active':''}`} href="/panel/metins"><Gem size={18}/><span>Metinler</span></a>
        <a className={`nav-item ${kind==='items'?'active':''}`} href="/panel/items"><PackageOpen size={18}/><span>İtemler</span></a>
        <PanelExtendedNav/>
      </nav>
      <div className="server-card"><div className="server-title"><CircleGauge size={16}/> Kayıt Güvencesi</div><div className="server-row"><span><i/> Proto kaynağı</span><strong>Doğrulandı</strong></div><div className="server-row"><span><i/> Düzenleme</span><strong>İzinli</strong></div><small>Değerler yalnızca Düzenle düğmesiyle açılır.</small></div>
      <div className="admin-card"><div className="avatar">{initials}</div><div><strong>{user.name}</strong><span>{user.email}</span></div></div>
    </aside>
    <main className="main detail-main">
      <header className="topbar detail-topbar"><a className="detail-back" href={kind==='groups'?'/panel/mobs':`/panel/${kind}`}><ArrowLeft/> {ui.list}</a><div className="top-actions"><div className="live-badge"><i/> OYUN SENKRONU</div><div className="avatar small">{initials}</div></div></header>
      <div className="content detail-content">
        {loading ? <div className="loading"><LoaderCircle className="spin"/><strong>Proto kaydı açılıyor</strong><span>Kaynak ve panel değişiklikleri birleştiriliyor.</span></div> : error ? <section className="detail-error"><X/><h1>Kayıt açılamadı</h1><p>{error}</p><a className="secondary" href={kind==='groups'?'/panel/mobs':`/panel/${kind}`}>Listeye dön</a></section> : row && <>
          <section className={`detail-hero ${ui.accent}`}>
            <div className="detail-identity"><div className="detail-icon"><Icon/></div><div><div className="detail-badges"><span>VNUM #{row.vnum}</span><span className={row.source==='Düzenlendi'?'changed':''}>{row.source}</span><span className={row.enabled===0?'disabled':'enabled'}>{row.enabled===0?'Pasif':'Aktif'}</span></div><h1>{row.name}</h1><p>{heroDescription(kind, row)}</p></div></div>
            <div className="detail-actions">{editing ? <><button className="secondary" onClick={()=>{setForm(row);setEditing(false)}}><X/> Vazgeç</button><button className="primary" disabled={saving} onClick={()=>void save()}>{saving?<LoaderCircle className="spin"/>:<Save/>} Kaydet ve Oyuna Yayınla</button></> : kind!=='groups'&&<button className="primary" onClick={()=>setEditing(true)}><Edit3/> Düzenle</button>}</div>
          </section>

          {editing ? <section className="panel detail-editor"><header><div><span>DÜZENLEME MODU</span><h2>Değiştirilebilir değerler</h2></div><Shield/></header><div className="detail-edit-grid">{editable.map(([key,label,type='text'])=><label key={key}><span>{label}</span><input required type={type} step={type==='number'?'any':undefined} value={form[key]??''} onChange={(event)=>setForm({...form,[key]:type==='number'?Number(event.target.value):event.target.value})}/></label>)}<label className="detail-toggle"><span>Kayıt durumu</span><button type="button" className={form.enabled===false||form.enabled===0?'off':'on'} onClick={()=>setForm({...form,enabled:form.enabled===false||form.enabled===0})}><i/>{form.enabled===false||form.enabled===0?'Pasif':'Aktif'}</button></label></div><footer><CheckCircle2/><span>Kaydetmeden önce değişiklikleri kontrol et. Kayıt, canlı oyun yapılandırmasına aktarılır.</span></footer></section> : <div className="detail-sections">{sections.map((section)=><section className="panel detail-section" key={section.title}><header><div className={`section-icon ${section.tone}`}>{section.icon}</div><div><span>{section.kicker}</span><h2>{section.title}</h2></div></header><div className="detail-field-grid">{section.fields.map(([key,label,type])=><DetailField key={key} label={label} value={row[key]} flags={type==='flags'}/>)}</div></section>)}</div>}
        </>}
      </div>
    </main>
    {toast&&<div className="toast">{toast}</div>}
  </div>;
}

function DetailField({ label, value, flags }: { label: string; value: unknown; flags?: boolean }) {
  const normalized = Array.isArray(value) ? value : flags ? String(value || '').split(/[|,]/).filter(Boolean) : null;
  return <div className={`detail-field ${flags?'wide':''}`}><span>{label}</span>{normalized ? <div className="detail-chips">{normalized.length ? normalized.map((part)=><b key={String(part)}>{String(part)}</b>) : <em>Tanımlı değil</em>}</div> : <strong>{formatValue(value)}</strong>}</div>;
}

function formatValue(value: unknown) {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'number') return value.toLocaleString('tr-TR');
  return String(value).replaceAll('_', ' ');
}

function heroDescription(kind: Kind, row: Row) {
  if (kind === 'items') return `${row.category} · ${formatValue(row.type)} · ${formatValue(row.subtype)}`;
  if (kind === 'groups') return `${row.memberCount ?? row.members?.length ?? 0} üyeli mob grubu · Lider VNUM #${row.leaderVnum}`;
  return `${formatValue(row.kind)} · ${formatValue(row.rank)} · Seviye ${formatValue(row.level)}${row.folder ? ` · ${row.folder}` : ''}`;
}

function editFields(kind: Kind): Field[] {
  if (kind === 'items') return [['name','İtem adı'],['description','Açıklama'],['category','Kategori'],['buy_price','Alış fiyatı','number'],['sell_price','Satış fiyatı','number'],['refineVnum','Geliştirme sonucu VNUM','number'],['refineSet','Geliştirme seti','number'],['magicPct','Büyü yüzdesi','number'],['specular','Parlaklık','number'],['socket','Soket','number']];
  return [['name','Ad'],['rank','Rütbe'],['level','Seviye','number'],['hp','HP','number'],['exp','EXP','number'],['min_damage','Minimum hasar','number'],['max_damage','Maksimum hasar','number'],['defense','Savunma','number'],['attack_speed','Saldırı hızı','number'],['move_speed','Hareket hızı','number'],['aggressiveSight','Agresif görüş','number'],['attackRange','Saldırı menzili','number'],['minGold','Minimum Yang','number'],['maxGold','Maksimum Yang','number'],['regenCycle','Yenilenme döngüsü','number'],['regenPercent','Yenilenme yüzdesi','number']];
}

function detailSections(kind: Kind, row: Row) {
  if (kind === 'items') return [
    { kicker:'TEMEL KAYIT', title:'Kimlik ve Sınıflandırma', tone:'blue', icon:<PackageOpen/>, fields:[['vnum','VNUM'],['name','İtem adı'],['description','Açıklama'],['category','Kategori'],['type','Proto türü'],['subtype','Alt tür'],['size','Boyut'],['source','Veri kaynağı']] as Field[] },
    { kicker:'EKONOMİ', title:'Fiyatlandırma', tone:'gold', icon:<Sparkles/>, fields:[['buy_price','NPC alış fiyatı'],['sell_price','NPC satış fiyatı'],['stackable','Yığınlanabilir'],['refineVnum','Geliştirme sonucu VNUM'],['refineSet','Geliştirme seti']] as Field[] },
    { kicker:'KULLANIM KURALLARI', title:'Bayraklar ve Sınırlar', tone:'red', icon:<Shield/>, fields:[['antiFlags','Anti bayrakları','flags'],['flags','İtem bayrakları','flags'],['wearFlags','Takılma yuvaları','flags'],['immuneFlags','Bağışıklık bayrakları','flags'],['limitType0','Sınır türü 1'],['limitValue0','Sınır değeri 1'],['limitType1','Sınır türü 2'],['limitValue1','Sınır değeri 2']] as Field[] },
    { kicker:'TEKNİK DEĞERLER', title:'Eklentiler ve Değer Alanları', tone:'green', icon:<CircleGauge/>, fields:[['magicPct','Büyü yüzdesi'],['addonType0','Eklenti türü 1'],['addonValue0','Eklenti değeri 1'],['addonType1','Eklenti türü 2'],['addonValue1','Eklenti değeri 2'],['addonType2','Eklenti türü 3'],['addonValue2','Eklenti değeri 3'],['value0','Value 0'],['value1','Value 1'],['value2','Value 2'],['value3','Value 3'],['value4','Value 4'],['value5','Value 5'],['specular','Parlaklık'],['socket','Soket']] as Field[] },
  ];
  if (kind === 'groups') return [
    { kicker:'GRUP KAYDI', title:'Grup Bilgileri', tone:'green', icon:<UsersRound/>, fields:[['vnum','Grup VNUM'],['name','Grup adı'],['leaderVnum','Lider VNUM'],['memberCount','Üye sayısı'],['members','Üye VNUM’ları','flags']] as Field[] },
  ];
  return [
    { kicker:'TEMEL KAYIT', title:'Kimlik ve Sınıflandırma', tone:'red', icon:<Skull/>, fields:[['vnum','VNUM'],['name','Ad'],['kind','Katalog türü'],['protoType','Proto sınıfı'],['rank','Rütbe'],['battleType','Savaş türü'],['level','Seviye'],['size','Boyut'],['folder','Model klasörü'],['source','Veri kaynağı']] as Field[] },
    { kicker:'SAVAŞ DEĞERLERİ', title:'Can, Hasar ve Ödül', tone:'gold', icon:<Swords/>, fields:[['hp','HP'],['exp','EXP'],['min_damage','Minimum hasar'],['max_damage','Maksimum hasar'],['defense','Savunma'],['minGold','Minimum Yang'],['maxGold','Maksimum Yang'],['dropItemGroup','Drop item grubu']] as Field[] },
    { kicker:'DAVRANIŞ', title:'Hız, Yenilenme ve Menzil', tone:'blue', icon:<Sparkles/>, fields:[['attack_speed','Saldırı hızı'],['move_speed','Hareket hızı'],['aggressiveHpPct','Agresif HP yüzdesi'],['aggressiveSight','Agresif görüş'],['attackRange','Saldırı menzili'],['regenCycle','Yenilenme döngüsü'],['regenPercent','Yenilenme yüzdesi'],['enabled','Kayıt durumu']] as Field[] },
    { kicker:'ÖZELLİKLER', title:'Statlar ve Bayraklar', tone:'green', icon:<Shield/>, fields:[['st','STR'],['dx','DEX'],['ht','VIT'],['iq','INT'],['aiFlags','AI bayrakları','flags'],['raceFlags','Irk bayrakları','flags'],['immuneFlags','Bağışıklık bayrakları','flags']] as Field[] },
  ];
}
