import { requireChatGPTUser } from '../../../chatgpt-auth';
import { AdvancedModulePanel } from '../../../../components/AdvancedModulePanel';

export const dynamic = 'force-dynamic';

const allowed = new Set(['accounts','characters','gm','bans','warps','chests','exp','biology','fishing','markets','trades','server-status','history']);

export default async function AdvancedModulePage({ params }:{params:Promise<{module:string}>}) {
  const { module } = await params;
  const safeModule = allowed.has(module) ? module : 'history';
  const user = await requireChatGPTUser(`/panel/system/${safeModule}`);
  return <AdvancedModulePanel moduleId={safeModule} user={{name:user.displayName,email:user.email}}/>;
}
