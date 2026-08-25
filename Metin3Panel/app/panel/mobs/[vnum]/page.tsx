import type { Metadata } from 'next';
import { requireChatGPTUser } from '../../../chatgpt-auth';
import { CatalogDetailPage } from '../../../../components/CatalogDetailPage';
import { findMob } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

export async function generateMetadata({ params }: { params: Promise<{ vnum: string }> }): Promise<Metadata> {
  const { vnum } = await params; const row = findMob(Number(vnum));
  const title = row ? `#${row.vnum} ${row.name} · Mob Detayı` : 'Mob Detayı';
  const description = row ? `${row.name} için seviye, HP, EXP, hasar ve davranış değerleri.` : 'Metin 3 mob proto kaydı.';
  return { title, description, openGraph: { title, description, images: [] }, twitter: { title, description, images: [] } };
}

export default async function MobDetailRoute({ params }: { params: Promise<{ vnum: string }> }) {
  const { vnum } = await params; const user = await requireChatGPTUser(`/panel/mobs/${vnum}`);
  return <CatalogDetailPage kind="mobs" vnum={Number(vnum)} user={{ name: user.displayName, email: user.email }}/>;
}
