import type { Metadata } from 'next';
import { requireChatGPTUser } from '../../../chatgpt-auth';
import { CatalogDetailPage } from '../../../../components/CatalogDetailPage';
import { findMob } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

export async function generateMetadata({ params }: { params: Promise<{ vnum: string }> }): Promise<Metadata> {
  const { vnum } = await params; const row = findMob(Number(vnum));
  const title = row ? `#${row.vnum} ${row.name} · Metin Detayı` : 'Metin Detayı';
  const description = row ? `${row.name} için seviye, HP, EXP, hasar ve savunma değerleri.` : 'Metin 3 metin taşı proto kaydı.';
  return { title, description, openGraph: { title, description, images: [] }, twitter: { title, description, images: [] } };
}

export default async function MetinDetailRoute({ params }: { params: Promise<{ vnum: string }> }) {
  const { vnum } = await params; const user = await requireChatGPTUser(`/panel/metins/${vnum}`);
  return <CatalogDetailPage kind="metins" vnum={Number(vnum)} user={{ name: user.displayName, email: user.email }}/>;
}
