import type { StarterDoor } from '../starters';
import type { ReceiptScope } from './PressReceiptLine';

/** The window's own door to what a receipt made: the repository's page on Repositories, or the quest on Quests. */
export const goDoorOf = (scope: ReceiptScope, id: string): StarterDoor => (scope === 'repository' ? { view: 'projects', item: id } : { view: 'quests', item: id });
