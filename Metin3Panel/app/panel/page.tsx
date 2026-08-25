import { requireChatGPTUser } from '../chatgpt-auth';
import { Metin3AdminPanel } from '../../components/Metin3AdminPanel';

export const dynamic = 'force-dynamic';

export default async function AdminPage() {
  const user = await requireChatGPTUser('/panel');
  return <Metin3AdminPanel user={{ name: user.displayName, email: user.email }} />;
}
