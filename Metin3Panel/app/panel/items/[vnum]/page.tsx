import type { Metadata } from 'next';
import { requireChatGPTUser } from '../../../chatgpt-auth';
import { CatalogDetailPage } from '../../../../components/CatalogDetailPage';
import { findItem } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

export async function generateMetadata({ params }: { params: Promise<{ vnum: string }> }): Promise<Metadata> {
  const { vnum } = await params; const row = findItem(Number(vnum));
  const title = row ? `#${row.vnum} ${row.name} · İtem Detayı` : 'İtem Detayı';
  const description = row ? `${row.name} için tür, fiyat, kullanım ve geliştirme değerleri.` : 'Metin 3 item proto kaydı.';
  return { title, description, openGraph: { title, description, images: [] }, twitter: { title, description, images: [] } };
}

export default async function ItemDetailRoute({ params }: { params: Promise<{ vnum: string }> }) {
  const { vnum } = await params; const user = await requireChatGPTUser(`/panel/items/${vnum}`);
  return <CatalogDetailPage kind="items" vnum={Number(vnum)} user={{ name: user.displayName, email: user.email }}/>;
}
