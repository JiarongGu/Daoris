import type { Meta, StoryObj } from '@storybook/react-vite';
import { useLayoutEffect, useMemo, useState } from 'react';
import { I18nextProvider } from 'react-i18next';
import { COMMANDS, type CommandState, MENUS, type MenuId, shortcutGroups, type Translate } from '../commands';
import { isPress } from '../shortcuts';
import { readerIn } from '../storyLanguage';
import { menuRows } from './appMenus';
import { AppMenuBar, FOLD_OPEN, foldsMenus } from './AppMenu';
import { BrowserDoor } from './BrowserDoor';
import { CommandCenter } from './CommandCenter';
import { AppStrip } from './frame';
import { KeyboardShortcuts } from './KeyboardShortcuts';
import { LayoutToggles } from './LayoutToggles';
import { ATTENDED, IN_A_BROWSER, menuWorld, OPEN_QUEST } from './menuFixtures';

// The menu bar (UX7a, D152): the strip as the window draws it, with each menu open over the page, built from the one
// table the palette and the keys read. `theme`, `language` and `world` are args, so one story is photographed in both
// themes and both languages at 1546 and 680 px; `width` sets the window's, and below the names' room they fold (UX7a2).

type World = 'shell' | 'attended' | 'quest' | 'browser';

const WORLDS: Record<World, Partial<CommandState>> = {
  shell: {},
  attended: ATTENDED,
  quest: OPEN_QUEST,
  browser: IN_A_BROWSER,
};

/** The theme on the document, as a person chooses it (D66), put back when the story goes. */
function useTheme(theme: 'light' | 'dark') {
  useLayoutEffect(() => {
    const root = document.documentElement;
    const was = root.dataset.theme;
    root.dataset.theme = theme;
    return () => {
      if (was === undefined) delete root.dataset.theme;
      else root.dataset.theme = was;
    };
  }, [theme]);
}

function Strip({ menu, world, mnemonics, chinese, width }: {
  menu: MenuId | typeof FOLD_OPEN | null; world: World; mnemonics: boolean; chinese: boolean; width?: number;
}) {
  // A reader of its own in either language, so English is English whatever the window speaks; neither is remembered (STORY2).
  const reader = useMemo(() => readerIn(chinese ? 'zh' : 'en'), [chinese]);
  const t = reader.t.bind(reader) as Translate;
  const [open, setOpen] = useState<string | null>(menu);
  const attached = world !== 'browser';
  const entries = menuWorld({ ...WORLDS[world], language: chinese ? 'zh' : 'en' }, t);
  const menus = MENUS.filter((each) => !each.shell || attached).map((each) => ({
    id: each.id, label: t(each.label), ...(attached ? { letter: each.letter } : {}), items: menuRows(entries, each.id),
  }));
  // A story's window is the width it is given (UX7a2): the strip folds its menus where the window would.
  const compact = width !== undefined && foldsMenus(width);
  return (
    <I18nextProvider i18n={reader}>
      <div className="flex h-[40rem] flex-col bg-page text-ink" style={width !== undefined ? { width } : undefined}>
        <AppStrip
          captionRoom={attached}
          menus={(
            <AppMenuBar
              label={t('menu.bar')} menus={menus} open={open} onOpen={setOpen} onChoose={() => {}} mnemonics={mnemonics}
              compact={compact} foldLabel={t('menu.fold')}
            />
          )}
          center={<CommandCenter scope={t('scope.every', { count: 2 })} shortcut="Ctrl K" onOpen={() => {}} label={t('palette.open')} />}
          trailing={(
            <div className="flex items-center gap-2">
              {attached && <BrowserDoor onOpen={() => {}} />}
              {/* At a story's own width, as the window's media query takes them off below 54rem. */}
              {(width === undefined || width >= 54 * 16) && (
                <div className="flex items-center max-[54rem]:hidden">
                  <LayoutToggles
                    regions={attached ? ['list', 'panel', 'right'] : ['list']}
                    list={t('layout.list.quests')}
                    closed={{ list: false, panel: false, right: true }}
                    onToggle={() => {}}
                  />
                </div>
              )}
            </div>
          )}
        />
      </div>
    </I18nextProvider>
  );
}

function MenuBarStory({ menu, world = 'shell', theme = 'light', language = 'en', mnemonics = false, width }: {
  menu: MenuId | typeof FOLD_OPEN | 'none';
  world?: World;
  theme?: 'light' | 'dark';
  language?: 'en' | 'zh';
  mnemonics?: boolean;
  /** The window's width in CSS px; absent, the story's whole width. */
  width?: number;
}) {
  useTheme(theme);
  // Drawn anew for each set of args, so the menu named opens as the story opens.
  return (
    <Strip
      key={`${menu}-${world}-${language}-${width ?? 'whole'}`}
      menu={menu === 'none' ? null : menu}
      world={world}
      mnemonics={mnemonics}
      chinese={language === 'zh'}
      width={width}
    />
  );
}

const meta: Meta<typeof MenuBarStory> = {
  title: 'Chrome/MenuBar',
  component: MenuBarStory,
  parameters: { layout: 'fullscreen' },
  args: { world: 'shell', theme: 'light', language: 'en', mnemonics: false },
  argTypes: {
    world: { control: 'inline-radio', options: ['shell', 'attended', 'quest', 'browser'] },
    theme: { control: 'inline-radio', options: ['light', 'dark'] },
    language: { control: 'inline-radio', options: ['en', 'zh'] },
  },
};
export default meta;

type Story = StoryObj<typeof MenuBarStory>;

/** The bar closed: seven menus, the command center, the toggles and the caption room. */
export const Closed: Story = { args: { menu: 'none' } };

/** Alt held: each menu's letter, underlined, and after the name in 中文. */
export const AltHeld: Story = { args: { menu: 'none', mnemonics: true } };

/** Workspace: the new things, the scope, the repositories, the workspace's setup, Settings. */
export const Workspace: Story = { args: { menu: 'workspace' } };

/** Edit: a field's six with their keys (off with no field), Find, Search knowledge, Copy ID. */
export const Edit: Story = { args: { menu: 'edit' } };

/** View: Commands, the regions ticked as shown, the views, the windows, Theme and Language, the index. */
export const View: Story = { args: { menu: 'view' } };

/** Go: the places with Ctrl+1 to Ctrl+8, the one in front ticked, then the regions. */
export const Go: Story = { args: { menu: 'go' } };

/** Run with nothing in front: each record group's acts in their place, off, the tip saying what to open. */
export const Run: Story = { args: { menu: 'run' } };

/** Run with a session attended: its stop and its own window apply, as its header offers them. */
export const RunOnASession: Story = { args: { menu: 'run', world: 'attended' } };

/** Run with an open quest: *Take*, *Mark done* and *Decline…* apply. */
export const RunOnAQuest: Story = { args: { menu: 'run', world: 'quest' } };

/** Terminal: a shell's alone. */
export const Terminal: Story = { args: { menu: 'terminal' } };

/** Help: Ask Daoris and Quick Ask, Setup, Keyboard shortcuts, the machine log, Update, About. */
export const Help: Story = { args: { menu: 'help' } };

/** A browser: six menus, no machine verb, no key a browser keeps (D152 §3.6). */
export const BrowserWorkspace: Story = { args: { menu: 'workspace', world: 'browser' } };

/** A browser's Go: its places, no keys. */
export const BrowserGo: Story = { args: { menu: 'go', world: 'browser' } };

/** At a 680 px window: the seven names still, the command center its glyph, the layout toggles gone (D152 §3.1). */
export const At680: Story = { args: { menu: 'none', width: 680 } };

/**
 * At a 560 px window (UX7a2): narrower than the seven names need, so they fold into one menu, VS Code's ☰, beside the
 * command center's glyph, the browser's door and the window's buttons.
 */
export const FoldedAt560: Story = { args: { menu: 'none', width: 560 } };

/** The fold open: its rows are the seven menus in the bar's order, each opening to its side. */
export const FoldedOpen: Story = { args: { menu: FOLD_OPEN, width: 560 } };

/** The fold opened at Run, as Alt+R opens it: Run's rows to its side, its record groups named. */
export const FoldedAtRun: Story = { args: { menu: 'run', width: 560 } };

/** Alt held with the fold open: each menu's letter on its row, underlined, and after the name in 中文. */
export const FoldedAltHeld: Story = { args: { menu: FOLD_OPEN, width: 560, mnemonics: true } };

/** Help › Keyboard shortcuts: every key by menu. */
export const Shortcuts: Story = {
  args: { menu: 'none' },
  render: ({ theme = 'light', language = 'en', world = 'shell' }) => <ShortcutsStory theme={theme} language={language} world={world} />,
};

/**
 * Which of the table's keys reach the page in the engine the story runs in (D152 §3.4: found, not assumed). Press each:
 * its row says *reached* when the page heard it, and the page takes it, so the engine's own use of it shows as no row
 * lit. Opened in the window's engine, it answers for the window; in a browser, for that browser.
 */
export const KeysReachingThePage: Story = {
  args: { menu: 'none' },
  render: ({ theme = 'light' }) => <KeyProbe theme={theme} />,
};

function KeyProbe({ theme }: { theme: 'light' | 'dark' }) {
  useTheme(theme);
  const keys = useMemo(() => [
    ...COMMANDS.flatMap((spec) => (spec.keys ?? []).map((key) => (typeof key === 'string' ? key : key.combo))
      .filter((combo) => !/^Ctrl\+[ZYXCVA]$/.test(combo))
      .map((combo) => ({ combo, id: spec.id }))),
    { combo: 'Alt', id: 'the menu bar' }, { combo: 'F10', id: 'the menu bar' }, { combo: 'Alt+W', id: 'menu.workspace' },
  ], []);
  const [reached, setReached] = useState<ReadonlySet<string>>(new Set());
  useLayoutEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const hit = keys.find(({ combo }) => (combo === 'Alt' ? event.key === 'Alt' : isPress(event, combo)));
      if (!hit) return;
      event.preventDefault();
      setReached((was) => new Set([...was, hit.combo]));
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [keys]);
  return (
    <div className="bg-page p-4 text-ink">
      <table className="text-small">
        <tbody>
          {keys.map(({ combo, id }) => (
            <tr key={`${combo}-${id}`}>
              <td className="pr-4 font-mono">{combo}</td>
              <td className="pr-4 text-ink-soft">{id}</td>
              <td className={reached.has(combo) ? 'text-accent' : 'text-ink-faint'}>{reached.has(combo) ? 'reached' : '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ShortcutsStory({ theme, language, world }: { theme: 'light' | 'dark'; language: 'en' | 'zh'; world: World }) {
  useTheme(theme);
  const reader = useMemo(() => readerIn(language), [language]);
  const t = reader.t.bind(reader) as Translate;
  return (
    <I18nextProvider i18n={reader}>
      <KeyboardShortcuts groups={shortcutGroups(menuWorld(WORLDS[world], t), t)} browser={world === 'browser'} onClose={() => {}} />
    </I18nextProvider>
  );
}
