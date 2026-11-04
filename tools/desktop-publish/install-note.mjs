import { join } from 'node:path';
import { running } from '../processes.mjs';
import { CONNECTOR_HOME, HOST_HOME, LAUNCHER, MARKER_HEADER, SHELL_EXE, SHELL_FILES, SHELL_HOME } from './names.mjs';
import { HOME, OFFERED_PLUGINS, PLUGIN_OFFERS, RESOURCES } from './lay.mjs';
import { CLI_BIN, CLI_HOME, CLI_LAUNCHERS, DRIVER_HOME } from './launchers.mjs';
import { STAGE, STAGED, SWAP_JOURNAL } from './staging.mjs';

/**
 * What an install's `INSTALLED.md` says: what this folder is, for whoever opens it in six months. It is
 * also the marker {@link isInstall} reads, so it opens with {@link MARKER_HEADER}. Written by the publish
 * rather than tracked, because the file belongs to the install; it names no machine path.
 *
 * The pinning section is D108's §2 (LEFT1): the window names Daoris's taskbar id, so a pin made from
 * it starts the launcher here, and a pin made on the launcher in Explorer carries no id and stays a
 * button apart from the running window.
 */
export function installedNote() {
  return `${MARKER_HEADER}

Published from a Daoris workspace by \`tools/desktop-publish.mjs\`.

## What is here

| | |
|---|---|
| \`${LAUNCHER}\` | **the application** — the only thing to run. A small launcher that starts \`${[...SHELL_HOME, SHELL_EXE].join('/')}\`. |
| \`${SHELL_HOME[0]}/\` | the application itself, on the Chromium it carries (its files are listed in \`${SHELL_FILES.join('/')}\`), which is also Daoris's own browser; when published with \`--service\`, the HTTP host in \`${HOST_HOME.slice(1).join('/')}/\` and, in \`${CONNECTOR_HOME.slice(1).join('/')}/\`, the knowledge connector a session on the protocol door is handed, each run ahead of any in \`bin/\` under the home; and Daoris's own example plugins in \`${PLUGIN_OFFERS.slice(1).join('/')}/\` (${OFFERED_PLUGINS.join(', ')}), offered in the Plugins place and by \`daoris plugin list\`, none installed until you install one; and \`${RESOURCES.slice(1).join('/')}\`, the list of where each version of the tools Daoris runs downloads from, read and never rewritten; and the command, \`daoris\`, in \`${CLI_BIN.slice(1).join('/')}/\` (below), which runs the desktop's program in \`${DRIVER_HOME.slice(1).join('/')}/\` and hands the doctrine commands to the package in \`${CLI_HOME.slice(1).join('/')}/\`. Nothing to open. |
| \`${STAGE[0]}/\` | an update: a build staged with \`--stage\` in \`${STAGED.slice(1).join('/')}/\`, which the application installs once its work allows, and the launcher's record of the last swap, \`${SWAP_JOURNAL}\` (D139). Present only once something was staged. |
| \`${HOME}/\` | **your settings and records**, the Daoris home: your choices, the registry and the knowledge store, the conversations, the plugins you installed. Back it up: it is the only copy of each, and it holds no key, so it can be copied or handed to someone helping without handing over an account. |
| \`profiles/\` | **sign-ins**: the API and deployment keys, Daoris's browser's profile, the window's own memory (\`profiles/window/\`: its engine profile, its place and its theme) and the agents' accounts made since. Lose it and you sign in again; keep a copy only somewhere as private as this machine. |
| \`work/\` | where agents work and what records keep for them: session trees, Ask Daoris's rooms, attachments. Work in progress lives only here; Daoris cleans it as work lands. |
| \`cache/\` | what Daoris fetched and fetches again: the agents' and the tools' versions, and the service's installed copy (\`cache/bin/\`). Never back it up: the next use downloads again. |
| \`run/\` | what a running Daoris holds: its lease, its address for a terminal, its locks and what it hands each session. Each entry expires by its own rule; deleting it is safe only while Daoris is closed. |
| \`logs/\` | the machine log, kept 30 days. |

Each of the folders after \`${HOME}/\` appears once something is written there (D172). Anything else in this
folder is not the application's — repositories it drives, typically — and a re-publish never touches it, nor
\`${HOME}/\` and the folders after it.

## Where everything lives

In this folder, in \`${HOME}/\` and the folders after it above, and nowhere under your user profile.
A start after an update moves what an older version kept in \`${HOME}/\` into the folders above, once, when
the update has settled and nothing else of this folder runs: the keys, the logs, the downloaded versions,
the browsers' and the window's profiles, and what a run holds (D172). Account homes, session trees, rooms,
kept files and \`${HOME}/bin/\` stay where they are, read there, because something outside Daoris names them by
path; the machine log says each move. On first start the application sets
\`DAORIS_HOME\` to \`${HOME}/\` for itself and — once, if your account has none — for your account,
which is how the \`daoris\` CLI on a terminal and the desktop are **two doors onto one machine**: what
one sets the other sees. When your account's \`DAORIS_HOME\` already names another folder — another
install's \`${HOME}/\`, or this one's before it moved — the application still runs on its own, leaves
that variable as it is, and says so on Settings' home row (D105): a terminal reads the folder the
variable names. A \`.daoris\` folder under your profile from an earlier version moves in on
that first start (its \`bin/\` stays: an install published with \`--service\` carries both hosts, and
\`publish:service --install\` lands copies here for a repository's own \`.mcp.json\`).
Deleting this folder removes the application and its machine — nothing else on the machine changes.

Starting it starts the driver loop, so **a drivable repository with an open quest gets a real agent
session.** \`daoris place list\` shows what this machine will drive.

## The command

\`${CLI_BIN.join('/')}/\` holds \`${CLI_LAUNCHERS[0]}\`, for Git Bash, and \`${CLI_LAUNCHERS[1]}\`, for Command Prompt and
PowerShell: Daoris's one command, \`daoris …\` (ONEWAY1ai), and \`daoris help\` names each of its groups. Each runs the
desktop's program this install carries in \`${DRIVER_HOME.join('/')}/\`, which needs no Node. Its doctrine commands
(\`check\`, \`sync\`, \`status\` and the rest) it hands to the \`daoris\` package this install carries in
\`${CLI_HOME.join('/')}/\`, packed from the same build as the application, as the release packs the one it publishes to npm
(\`daoris --version\` says which), on the \`node\` your PATH finds: they need Node.js 22 or later.
Every program Daoris starts finds it by its bare name: a driven session, a conversation, an intake, a hook,
a landing plugin and every shell of its terminal panel begin their PATH with \`${CLI_BIN.join('/')}/\`, because
this install's home, \`${HOME}/\`, sits beside \`${CLI_BIN[0]}/\` (D124). It reads the same home (\`DAORIS_HOME\`) the
application does; with none it takes this install's \`${HOME}/\`, and with no \`DAORIS_SERVICE_URL\` the service of the
Daoris running on that home, by the address the application leaves there while it runs (D171).
Nothing puts \`${CLI_BIN.join('/')}/\` on your account's PATH, so a terminal of yours outside Daoris still runs whatever
\`daoris\` you installed; run this one by its path there, from the repository it should look at. A re-publish
replaces these folders whole.

## Pinning it to the taskbar

Pin Daoris from its running window: right-click its button on the taskbar, then *Pin to taskbar*.
The window names Daoris's taskbar id (D108), so that pin starts \`${LAUNCHER}\` here and every later
Daoris window joins it as one button. A pin made on \`${LAUNCHER}\` itself, from Explorer, carries no id:
the launcher starts \`${[...SHELL_HOME, SHELL_EXE].join('/')}\` and exits, and Windows cannot tell that
the window belongs to that pin, so the running window shows as a second button beside it. If you have
a pin of that kind, unpin it, start Daoris, and pin its running window once.

## Updating it

Stage a new build beside it while it runs: \`npm run publish:desktop -- --to <this folder> --service --stage\`. The
application then starts nothing new, lets the running sessions end or park, closes, and \`${LAUNCHER}\` checks the
staged build, swaps \`${SHELL_HOME[0]}/\` and starts it again, putting the build before it back if the new one does not
come up (D139). The banner in the window and \`daoris update --when-idle|--now|--cancel\` say when.
While it drains the application starts nothing new and holds the presses that would start work;
\`daoris update --cancel\` stages the build *not now* (installed at the next start, or \`update --when-idle\` later).
Re-publishing over this folder with the application closed still works, with \`--service\` while it carries the hosts
(a re-publish without it is refused rather than leave the last build's); nothing here is edited by hand.
`;
}

// ---------------------------------------------------------------------------------------------
// GATE3: the full set before the install
