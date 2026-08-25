import { requireChatGPTUser } from './chatgpt-auth';
import { Metin3AdminPanel } from '../components/Metin3AdminPanel';

export const dynamic = 'force-dynamic';

export default async function Home() {
  const user = await requireChatGPTUser('/');
  return <Metin3AdminPanel user={{ name: user.displayName, email: user.email }} />;
}
