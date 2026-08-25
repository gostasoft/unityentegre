import type { Metadata } from 'next';
import { requireChatGPTUser } from '../../../chatgpt-auth';
import { CatalogDetailPage } from '../../../../components/CatalogDetailPage';
import { findGroup } from '../../../../lib/proto-catalog';

export const dynamic = 'force-dynamic';

export async function generateMetadata({ params }: { params: Promise<{ vnum: string }> }): Promise<Metadata> {
  const { vnum } = await params; const row = findGroup(Number(vnum));
  const title = row ? `#${row.vnum} ${row.name} · Grup Detayı` : 'Mob Grubu Detayı';
  const description = row ? `${row.name} grubunun lideri ve ${row.members.length} üyesi.` : 'Metin 3 mob grup kaydı.';
  return { title, description, openGraph: { title, description, images: [] }, twitter: { title, description, images: [] } };
}

export default async function GroupDetailRoute({ params }: { params: Promise<{ vnum: string }> }) {
  const { vnum } = await params; const user = await requireChatGPTUser(`/panel/groups/${vnum}`);
  return <CatalogDetailPage kind="groups" vnum={Number(vnum)} user={{ name: user.displayName, email: user.email }}/>;
}
