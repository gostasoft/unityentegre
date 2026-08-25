import type { Metadata } from 'next';
import { requireChatGPTUser } from '../../../chatgpt-auth';
import { PlayerDetailPage } from '../../../../components/PlayerDetailPage';

export const dynamic = 'force-dynamic';
export const metadata: Metadata = { title:'Oyuncu İnceleme · Metin 3 Panel', description:'Oyuncu envanteri, ekonomi, hareket geçmişi ve güvenlik yaptırımları.' };

export default async function PlayerRoute({ params }:{params:Promise<{id:string}>}) {
  const { id } = await params; const user = await requireChatGPTUser(`/panel/players/${id}`);
  return <PlayerDetailPage playerId={Number(id)} user={{name:user.displayName,email:user.email}}/>;
}
