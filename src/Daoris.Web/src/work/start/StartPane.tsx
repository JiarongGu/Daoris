import { useCheckoutBranch } from '../../bridge/repositories/git';
import type { Session } from '../../data/api';
import type { useRegistry } from '../../data/queries';
import { type Notify, SESSION_ACTIVE } from '../../design/ui';
import { AddProjectDrawer, ImportFolderDrawer } from '../../projects/ProjectManage';
import { ownTree, sessionOrigin } from '../identity';
import type { HandOverRoster } from './HandOverAsk';
import { type CheckoutReader, NewConversation } from './NewConversation';
import { mergeRecent } from './startRecent';
import type { useNewStart } from './useNewStart';

/**
 * The composer's tree chip reads the checkout's branch through this: a refusal or a pending read is no answer, and
 * the chip says the checkout plain. A module-level hook, so the molecule's prop is stable.
 */
const readCheckout: CheckoutReader = (repository, workspace) => {
  const { query } = useCheckoutBranch({ repository, workspace });
  return { read: query.isError ? undefined : query.data, refresh: () => { void query.refetch(); } };
};

/**
 * *New conversation*, the main area while nothing is attended: its chips are the composer's own, and the drawers its picker
 * opens over Sessions are Projects' own.
 */
export function StartPane({ start, registered, sessions, agentRoster, defaultHarness, notify, onMakeTask, attend }: {
  start: ReturnType<typeof useNewStart>;
  registered: NonNullable<ReturnType<typeof useRegistry>['data']>;
  sessions: Session[] | undefined;
  agentRoster: HandOverRoster;
  defaultHarness?: string;
  notify: Notify;
  onMakeTask?: (text: string) => void;
  attend: (id: string) => void;
}) {
  const {
    composing, focusOwed, setFocusOwed, pressedFrom, adding, setAdding, importing, setImporting, pickNew, setPickNew, reveal, setReveal,
    refocus, listOpened, newDraft, setNewDraft, newCarry, setNewCarry, startRefusal, recentStarts, chosenStart, chooseStart, startChat,
    onStart, drawerDown,
  } = start;
  return (
    <>
      <NewConversation
        readCheckout={readCheckout}
        // A checkout on this machine is what makes a repository choosable: there is nowhere else to talk, and a teammate's
        // mirrored registration has no tree here. One without is listed and says so.
        repositories={registered
          .map((row) => ({ name: row.repository, workspace: row.workspace || 'default', checkout: Boolean(row.root) }))
          .sort((a, b) => a.name.localeCompare(b.name))}
        // A checkout an active session on this machine holds refuses a second one: with the session holding it.
        busy={Object.fromEntries((sessions ?? [])
          .filter((session) => SESSION_ACTIVE.has(session.state) && sessionOrigin(session) === null
            && !ownTree(session, registered.find((row) => row.repository === session.repository)?.root))
          .map((session) => [session.repository, session.id]))}
        recent={mergeRecent(recentStarts, sessions ?? [])}
        remembered={chosenStart}
        attended={pressedFrom}
        onChoose={chooseStart}
        harnesses={agentRoster.harnesses}
        labels={agentRoster.labels}
        names={agentRoster.names}
        defaultHarness={defaultHarness}
        accounts={agentRoster.accounts}
        orderOf={agentRoster.orderOf}
        draft={newDraft}
        onDraft={setNewDraft}
        carried={newCarry}
        onCarry={setNewCarry}
        pending={startChat.isPending}
        refusal={startRefusal}
        focus={(focusOwed ? composing : 0) + refocus}
        onFocused={() => setFocusOwed(false)}
        onStart={onStart}
        onOpenSession={attend}
        onAdd={() => setAdding(true)}
        onImport={() => setImporting(true)}
        pick={pickNew}
        reveal={reveal}
        onMakeTask={onMakeTask}
      />
      {adding && (
        // Once registered it is chosen, as Projects' list chooses what it registered, and the words stay.
        <AddProjectDrawer
          onClose={drawerDown(() => setAdding(false))}
          onAdded={(repository) => setPickNew((last) => ({ repository, n: (last?.n ?? 0) + 1 }))}
          notify={notify}
        />
      )}
      {importing && (
        <ImportFolderDrawer
          onClose={drawerDown(() => setImporting(false))}
          onImported={({ repositories: landed, workspace }) => {
            // One is chosen; several, or none, leave the choice to the person, in the list opened on where they landed.
            if (landed.length === 1) {
              setPickNew((last) => ({ repository: landed[0]!, n: (last?.n ?? 0) + 1 }));
              return;
            }
            listOpened.current = true;
            setReveal((last) => ({ workspace: landed.length > 1 ? workspace ?? '' : '', n: (last?.n ?? 0) + 1 }));
          }}
          notify={notify}
        />
      )}
    </>
  );
}
