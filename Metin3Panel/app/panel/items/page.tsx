import { requireChatGPTUser } from '../../chatgpt-auth';
import { ProtoCatalogPanel } from '../../../components/ProtoCatalogPanel';
export const dynamic = 'force-dynamic';
export default async function ItemsPage(){const user=await requireChatGPTUser('/panel/items');return <ProtoCatalogPanel kind="items" user={{name:user.displayName,email:user.email}}/>}
