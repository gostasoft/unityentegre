import Image from 'next/image';
import Link from 'next/link';
import { Download, LogIn, ShieldCheck, Sparkles, Swords } from 'lucide-react';
import styles from './site.module.css';

const menu = [
  ['Ana Sayfa', '/tr/'],
  ['Oyun Hakkında', '/tr/thegame'],
  ['Hesap Oluştur', '/tr/register'],
  ['Giriş Yap', '/tr/login'],
  ['Galeri', '/tr/media'],
  ['Sıralamalar', '/tr/rankings'],
  ['Destek', '/tr/support'],
];

export default function TurkishHomePage() {
  return (
    <main className={styles.siteShell}>
      <header className={styles.hero}>
        <div className={styles.heroArt} aria-hidden="true">
          <span>GÖRSEL: hero-bg.webp</span>
        </div>
        <nav className={styles.topbar} aria-label="Ana menü">
          <Link href="/tr/" className={styles.logoLink}>
            <Image src="/metin3-logo.png" alt="Metin 3" width={250} height={82} priority />
          </Link>
          <div className={styles.topLinks}>
            <Link href="/tr/thegame">OYUN</Link>
            <Link href="/tr/media">GALERİ</Link>
            <Link href="/tr/rankings">SIRALAMA</Link>
            <Link href="/panel">YÖNETİM</Link>
          </div>
          <Link href="/tr/login" className={styles.signIn}><LogIn size={17}/> GİRİŞ YAP</Link>
        </nav>
        <div className={styles.heroContent}>
          <p className={styles.kicker}><Sparkles size={15}/> EFSANE YENİDEN DOĞUYOR</p>
          <h1>ÜÇ İMPARATORLUK.<br/><span>TEK BİR KADER.</span></h1>
          <p>Metin taşlarının karanlığına karşı savaş, imparatorluğunu yükselt ve kendi destanını yaz.</p>
          <div className={styles.heroActions}>
            <Link href="/tr/register" className={styles.primaryButton}><ShieldCheck size={20}/> ÜCRETSİZ HESAP OLUŞTUR</Link>
            <Link href="/tr/download" className={styles.secondaryButton}><Download size={20}/> OYUNU İNDİR</Link>
          </div>
          <div className={styles.steps}>
            <span><b>01</b> Hesabını oluştur</span><i/><span><b>02</b> Oyunu indir</span><i/><span><b>03</b> Savaşa katıl</span>
          </div>
        </div>
        <div className={styles.scrollCue}><Swords size={16}/> AŞAĞI KAYDIR</div>
      </header>

      <section className={styles.portalFrame}>
        <aside className={styles.leftRail}>
          <p className={styles.railTitle}>MENÜ</p>
          {menu.map(([label, href]) => <Link key={href} href={href}>{label}<span>›</span></Link>)}
          <Link href="/tr/download" className={styles.downloadCard}>
            <Download size={24}/><span><small>HEMEN BAŞLA</small>OYUNU İNDİR</span>
          </Link>
        </aside>

        <section className={styles.mainPanel}>
          <div className={styles.sectionEyebrow}>METİN 3 DÜNYASINA HOŞ GELDİN</div>
          <h2>SAVAŞÇINI SEÇ, EFSANENİ YAZ</h2>
          <p className={styles.lead}>Uzak Doğu’nun üç büyük imparatorluğu arasındaki mücadelede yerini al. Büyüyen bir dünya, zorlu zindanlar ve gerçek oyuncularla şekillenen bir ekonomi seni bekliyor.</p>
          <div className={styles.previewGrid}>
            {['news-01.webp','news-02.webp','news-03.webp'].map((name, index) => (
              <article key={name} className={styles.previewCard}>
                <div><span>GÖRSEL<br/>{name}</span></div>
                <small>{index === 0 ? 'DUYURU' : index === 1 ? 'REHBER' : 'ETKİNLİK'}</small>
                <h3>{index === 0 ? 'Metin 3 macerası başlıyor' : index === 1 ? 'İmparatorluğunu savun' : 'Haftanın etkinlik takvimi'}</h3>
              </article>
            ))}
          </div>
        </section>

        <aside className={styles.rightRail}>
          <div className={styles.loginCard}>
            <p className={styles.railTitle}>OYUNCU GİRİŞİ</p>
            <label>Kullanıcı adı<input placeholder="Kullanıcı adın" /></label>
            <label>Şifre<input type="password" placeholder="••••••••" /></label>
            <button type="button">GİRİŞ YAP</button>
            <Link href="/tr/register">Yeni hesap oluştur</Link>
          </div>
          <div className={styles.serverStatus}><i/><span><small>SUNUCU DURUMU</small>METİN 3 • CH1</span><b>ÇEVRİMİÇİ</b></div>
        </aside>
      </section>
    </main>
  );
}
