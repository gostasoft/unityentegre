import { requireChatGPTUser } from '../../chatgpt-auth';
import { ProtoCatalogPanel } from '../../../components/ProtoCatalogPanel';
export const dynamic = 'force-dynamic';
export default async function MetinsPage(){const user=await requireChatGPTUser('/panel/metins');return <ProtoCatalogPanel kind="metins" user={{name:user.displayName,email:user.email}}/>}
