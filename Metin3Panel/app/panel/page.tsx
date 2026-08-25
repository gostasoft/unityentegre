import { requireChatGPTUser } from '../chatgpt-auth';
import { Metin3AdminPanel } from '../../components/Metin3AdminPanel';

export const dynamic = 'force-dynamic';

const allowedViews = new Set(['dashboard','world','shops','drops','players','events','settings']);

export default async function AdminPage({ searchParams }: { searchParams: Promise<{ view?: string }> }) {
  const user = await requireChatGPTUser('/panel');
  const { view } = await searchParams;
  return <Metin3AdminPanel user={{ name: user.displayName, email: user.email }} initialView={view && allowedViews.has(view) ? view : 'dashboard'} />;
}
