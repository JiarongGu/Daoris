import type * as React from 'react';
import { type NotificationsAt } from '../../notifications/NotificationsView';
import { type AttentionDoors } from '../../work/attention/AttentionBand';
import { type OpenPart } from '../opener';
import { type SettingsAnchor, type SettingsSection } from '../../settings/SettingsView';
import { type Attention } from '../../work/attention/AttentionRow';
import { outputsIntent } from '../../work/attention/attentionRun';
import { type FrameIntent, type View } from '../../menus/commands';

export function useAttentionDoors({ attached, openInWork, openAsk, openQuest, open, setWorkIntent, showNotifications, openSettings, openHelp }: {
  attached: boolean;
  openHelp?: () => void;
  openInWork: (session: string) => void;
  openAsk: (id: string) => void;
  openQuest: (id: string) => void;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
  setWorkIntent: React.Dispatch<React.SetStateAction<FrameIntent | null>>;
  showNotifications: (at?: Omit<NotificationsAt, "n"> | undefined) => void;
  openSettings: (section: SettingsSection, anchor?: SettingsAnchor | undefined) => void;
}) {
  // Each row's door: where its record is, beside the acts the row settles in place.
  const attentionDoors: AttentionDoors = {
    ...(attached ? { parked: (item) => openInWork(item.id) } : {}),
    proposal: (item) => openAsk(item.id),
    intake: (item) => openAsk(item.id),
    unanswerable: (item) => openQuest(item.id),
    'plan-question': (item) => { if (item.ask) openAsk(item.ask); },
    // A quest parked on its failed sessions opens its page, and its session from there.
    'parked-quest': (item) => openQuest(item.id),
    // The steward's stop opens its quest's page, as a parked quest does.
    'steward-stopped': (item) => openQuest(item.id),
    // A quest its role's policy holds opens its page, as a parked quest does.
    'policy-hold': (item) => openQuest(item.id),
    unclosed: (item) => openQuest(item.id),
    // A run held at a declined request opens its work quest's page, where the run and the request are read.
    'request-declined': (item) => { if (item.declined?.work) openQuest(item.declined.work); },
    // A run waiting on a person's task (HUMAN) opens its work quest's page, where the run is read.
    task: (item) => { if (item.task?.work) openQuest(item.task.work); },
    // A held git remote leads to Settings → Git on its host, where its identity is set up, signed in again or
    // compared; one whose push is refused, or whose repository names its own authentication, to that repository's git remote row,
    // where another identity or the person's own sign-ins are chosen. A browser has no such page.
    ...(attached ? {
      'git-held': (item: Attention) => {
        const held = item.git;
        const repository = held?.repositories[0];
        if (repository && (held.class === 'write-refused' || held.class === 'own-input')) {
          open('agents', null, { placeOf: repository, placeSection: 'sessions', placeRow: 'git' });
        } else open('settings', 'git', held?.host ? { gitHost: held.host } : {});
      },
    } : {}),
    // A git go-ahead opens its session, whose head draws it; one no session's marker named has its row alone.
    ...(attached ? { 'git-ask': (item: Attention) => { if (item.session) openInWork(item.session); } } : {}),
    ...(attached && openHelp ? { 'second-check': () => openHelp() } : {}),
    ...(attached ? { 'check-failed': (item: Attention) => { if (item.session) openInWork(item.session); } } : {}),
    // A go-ahead opens the ask it is asked on, where every go-ahead of it is listed.
    'go-ahead': (item) => { if (item.ask) openAsk(item.ask); },
    // A departure opens its quest's page, where what is required and how its done answered are quoted.
    departure: (item) => openQuest(item.id),
    // A set-up shown for review opens its step's page, where every set-up it said and the review's presses are.
    'set-up': (item) => openQuest(item.id),
    // A folder waiting on the person's trust opens what it holds: the quest's page, or the ask whose intake it is.
    // The row asks the trust question itself. Only a shell has one.
    ...(attached ? {
      trust: (item: Attention) => {
        if (item.quest) openQuest(item.quest);
        else if (item.ask) openAsk(item.ask);
      },
    } : {}),
    // An agent's proposal to widen the rules opens the rules it would change, where it is answered beside them:
    // what the agent may do, on its page. Only a shell reads the rules.
    ...(attached ? { rule: () => open('agents', null, { agentPart: 'rules' }) } : {}),
    // Work to review opens in Sessions on its run's review where the row carries the run: accepting needs looking. A shell's alone.
    ...(attached ? { review: (item: Attention) => { open('sessions', item.id); setWorkIntent(outputsIntent(item)); } } : {}),
    // A second opinion waiting on the person opens the session whose work it read, where its gate and every press are
    // A shell's alone: the opinion is this machine's.
    ...(attached ? { left: (item: Attention) => open('sessions', item.id) } : {}),
    ...(attached ? { opinion: (item: Attention) => open('sessions', item.id) } : {}),
    // An agent's ask for a helper opens the session that asked, whose head and foot carry its yes and no with its other waits.
    // A shell's alone: the asks are this machine's files.
    ...(attached ? { helper: (item: Attention) => { if (item.session) open('sessions', item.session); } } : {}),
    // An agent's ask to hand its session over opens that session, whose foot carries its note whole with its yes and no.
    // A shell's alone: the asks are this machine's files.
    ...(attached ? { 'hand-over': (item: Attention) => { if (item.session) open('sessions', item.session); } } : {}),
    // A tool call a session holds for the person's answer opens that session, whose head and foot carry its presses with its other
    // waits. A shell's alone: the holds are this machine's driver's.
    ...(attached ? { permission: (item: Attention) => { if (item.session) open('sessions', item.session); } } : {}),
    // A third answer alone held for the person opens the session whose line it would take on, whose head and foot carry its two presses
    // with its other waits. A shell's alone: the marks are this machine's driver's.
    ...(attached ? { answer: (item: Attention) => { if (item.session) open('sessions', item.session); } } : {}),
    // A session the watcher brings to the person opens that session, whose head carries *Hand over…* and *Stop…*. A
    // shell's alone: the watch is this machine's driver's.
    ...(attached ? { quiet: (item: Attention) => open('sessions', item.session ?? item.id) } : {}),
    // A session whose move back to the person the service refused opens that session, as its park would. A shell's
    // alone: the record is this machine's driver's.
    ...(attached ? { 'helper-ended': (item: Attention) => open('sessions', item.session ?? item.id) } : {}),
    // A session whose memory waits on the person opens that session, whose Memory section carries the decisions. A shell's
    // alone: the service's rows are read by this machine's driver. A sweep's end names no session and shows no door: its row decides it.
    ...(attached ? { memory: (item: Attention) => { if (item.session) open('sessions', item.session); } } : {}),
    // An integration's wait on the owner opens its repository's Integration tab, where its decide, its answer or its request
    // is pressed beside the candidate and its steps. A shell's alone: the integration record is this machine's driver's.
    // A declaration owed acceptance opens the repository's Setup instead, where Line and landing holds the Integration declaration to read and accept.
    ...(attached
      ? {
          integration: (item: Attention) => {
            const repository = item.integration?.repository ?? item.where;
            if (item.integration?.wait === 'declaration') open('agents', null, { placeOf: repository });
            else open('projects', repository, { tab: 'integration' });
          },
        }
      : {}),
    // A terminal's press waiting for the window opens its card in *Notifications*, where every value it
    // carries is read beside its Confirm. Brought into view, never pressed: the card's default stays no. A shell's alone.
    ...(attached ? { confirmation: (item: Attention) => showNotifications({ press: item.id }) } : {}),
    // A volume still below its free-space floor opens This Daoris's *Disk space* card, where *Free up space…* lists what else
    // could go before anything does. A shell's alone: the disk is the machine's.
    ...(attached ? { space: () => openSettings('space', 'daoris-space') } : {}),
    // A unit only the clean-up's press settles opens the workspace's Branches, where the session branches' list and
    // its presses are; a held attempt's presses are on its row. An unknown owner's branch named for a session the page holds opens that
    // session, whose page discards it too. A shell's alone.
    ...(attached ? {
      work: (item: Attention) => {
        if (item.held) return;
        if (item.cutFor) open('sessions', item.cutFor);
        else open('projects', null, { workspaceTab: 'branches' });
      },
    } : {}),
    // An account's row opens its agent's page at its accounts, where every account's act is. A shell's alone.
    ...(attached ? {
      'account-wait': (item: Attention) => open('agents', item.account?.agent ?? null, { agentPart: 'accounts' }),
      'signed-out': (item: Attention) => open('agents', item.account?.agent ?? null, { agentPart: 'accounts' }),
    } : {}),
  };
  return { attentionDoors };
}
