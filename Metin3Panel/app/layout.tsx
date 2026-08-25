import type { Metadata } from 'next';
import { Inter, Manrope } from 'next/font/google';
import './globals.css';

const inter = Inter({ variable: '--font-body', subsets: ['latin', 'latin-ext'] });
const manrope = Manrope({ variable: '--font-display', subsets: ['latin', 'latin-ext'] });

export const metadata: Metadata = {
  metadataBase: new URL(process.env.SITE_URL ?? 'http://localhost:3000'),
  title: 'Metin 3 Yönetim Paneli',
  description: 'Metin 3 oyun dünyası, oyuncu, ekonomi ve etkinlik yönetim merkezi.',
  openGraph: {
    title: 'Metin 3 Yönetim Paneli',
    description: 'Oyun dünyası, ekonomi, oyuncu ve etkinlik yönetim merkezi.',
    images: [{ url: '/og.png', width: 1730, height: 909, alt: 'Metin 3 Yönetim Paneli' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'Metin 3 Yönetim Paneli',
    description: 'Oyun dünyası, ekonomi, oyuncu ve etkinlik yönetim merkezi.',
    images: ['/og.png'],
  },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="tr"><body className={`${inter.variable} ${manrope.variable}`}>{children}</body></html>;
}
