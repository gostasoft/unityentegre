import { requireChatGPTUser } from '../../chatgpt-auth';
import { ProtoCatalogPanel } from '../../../components/ProtoCatalogPanel';
export const dynamic = 'force-dynamic';
export default async function MobsPage(){const user=await requireChatGPTUser('/panel/mobs');return <ProtoCatalogPanel kind="mobs" user={{name:user.displayName,email:user.email}}/>}
