import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type BeyondEntry } from './beyond';
import { routeKey } from '../../person/Confirmations';
import { type Place } from '../focus';
import { BeyondReceipt, beyondBeneath, entriesOfTurn } from './BeyondReceipt';

// One folded receipt under an Ask Daoris turn of what it looked at and did beyond its focus (`docs/2026-11-11-ask-scope-ux-design.md`
// §4), in both catalogues: what Daoris's doors saw, each place with *Make … the focus*.

const games: Place = { kind: 'workspace', workspace: 'Games' };
const work: Place = { kind: 'workspace', workspace: 'Work' };
const api: Place = { kind: 'repository', workspace: 'Work', repository: 'api' };

const search: BeyondEntry = { at: '2026-11-11T12:00:05Z', act: 'search', place: work, what: 'release', outcome: null };
const every: BeyondEntry = { at: '2026-11-11T12:00:06Z', act: 'search', place: { kind: 'machine' }, what: null, outcome: null };
const read: BeyondEntry = { at: '2026-11-11T12:00:07Z', act: 'read', place: api, what: 'README.md', outcome: null };
const pressed: BeyondEntry = { at: '2026-11-11T12:00:08Z', act: 'press', place: work, what: 'DAORIS.DRIVER.WORK_PAUSE', outcome: 'done' };

describe.each(['en', 'zh'])('the receipt in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('folds to the places it looked at, each once, and unfolds to what the doors saw', async () => {
    await i18n.changeLanguage(language);
    render(<BeyondReceipt entries={[search, every, read, pressed]} focus={games} next={null} onFocus={() => {}} />);
    const places = ['Work', i18n.t('help.beyond.place.every'), 'api'].join(i18n.t('help.beyond.joiner'));
    const fold = screen.getByRole('button', { name: i18n.t('help.beyond.fold', { places }) });
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByText(i18n.t('help.beyond.source'))).toBeNull();

    fireEvent.click(fold);
    expect(screen.getByText(i18n.t('help.beyond.source'))).toBeInTheDocument();
    const workspace = i18n.t('help.beyond.place.workspace', { workspace: 'Work' });
    const repository = i18n.t('help.beyond.place.repository', { repository: 'api', workspace: 'Work' });
    expect(screen.getByText(i18n.t('help.beyond.search', { place: workspace }))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('help.beyond.search', { place: i18n.t('help.beyond.place.every') }))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('help.beyond.read', { place: repository, what: 'README.md' }))).toBeInTheDocument();
    const act = i18n.t(routeKey('DAORIS.DRIVER', 'WORK_PAUSE'), { defaultValue: 'WORK_PAUSE' });
    expect(screen.getByText(i18n.t('help.beyond.did', { act, place: workspace, outcome: i18n.t('notifications.helpPressed.outcome.done') })))
      .toBeInTheDocument();
  });

  it('says how many a search found where its answer said so', async () => {
    await i18n.changeLanguage(language);
    render(<BeyondReceipt entries={[{ ...search, count: 3 }, { ...every, count: 1 }]} focus={games} next={null} onFocus={() => {}} defaultOpen />);
    const workspace = i18n.t('help.beyond.place.workspace', { workspace: 'Work' });
    expect(screen.getByText(i18n.t('help.beyond.searchFound', { place: workspace, count: 3 }))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('help.beyond.searchFound', { place: i18n.t('help.beyond.place.every'), count: 1 }))).toBeInTheDocument();
    if (language === 'en') expect(screen.getByText('Searched workspace Work · 3 found')).toBeInTheDocument();
    if (language === 'zh') expect(screen.getByText('搜索了工作区 Work · 找到 3 条')).toBeInTheDocument();
  });

  it("says a press that asked the person apart from one that ran, with its card's state", async () => {
    await i18n.changeLanguage(language);
    const asked: BeyondEntry = { at: '2026-11-11T12:00:09Z', act: 'asked', place: work, what: 'DAORIS.DRIVER.WORK_PAUSE', outcome: 'waiting' };
    render(<BeyondReceipt entries={[pressed, asked]} focus={games} next={null} onFocus={() => {}} defaultOpen />);
    const workspace = i18n.t('help.beyond.place.workspace', { workspace: 'Work' });
    const act = i18n.t(routeKey('DAORIS.DRIVER', 'WORK_PAUSE'), { defaultValue: 'WORK_PAUSE' });
    const state = i18n.t('notifications.helpPressed.outcome.waiting');
    expect(screen.getByText(i18n.t('help.beyond.asked', { act, place: workspace, state }))).toBeInTheDocument();
    if (language === 'en') expect(screen.getByText('Asked you: pause the work on this machine in workspace Work · Waiting')).toBeInTheDocument();
    if (language === 'zh') expect(screen.getByText('已请你确认：工作区 Work 中的暂缓本机上的这项工作 · 等待中')).toBeInTheDocument();
    expect(document.querySelectorAll('[data-beyond-act="asked"]')).toHaveLength(1);
    expect(document.querySelectorAll('[data-beyond-act="press"]')).toHaveLength(1);
  });

  it('makes a place the focus, and says one that already is, or waits to be', async () => {
    await i18n.changeLanguage(language);
    const onFocus = vi.fn();
    render(<BeyondReceipt entries={[search, read]} focus={games} next={api} onFocus={onFocus} defaultOpen />);
    fireEvent.click(screen.getByRole('button', { name: i18n.t('help.beyond.make', { place: 'Work' }) }));
    expect(onFocus).toHaveBeenCalledWith(work);
    const already = screen.getByRole('button', { name: i18n.t('help.beyond.already') });
    expect(already).toBeDisabled();
  });
});

describe('the receipts of a conversation', () => {
  it('draw none for a turn that stayed within its focus', () => {
    render(<BeyondReceipt entries={[]} focus={games} next={null} onFocus={() => {}} />);
    expect(screen.queryByRole('button')).toBeNull();
    cleanup();
  });

  it('take each entry into the turn whose words it followed, before the next words', () => {
    const entries = [search, read, pressed];
    expect(entriesOfTurn(entries, '2026-11-11T12:00:00Z', '2026-11-11T12:00:07Z')).toEqual([search]);
    expect(entriesOfTurn(entries, '2026-11-11T12:00:07Z', undefined)).toEqual([read, pressed]);
    expect(entriesOfTurn(entries, '2026-11-11T13:00:00Z', undefined)).toEqual([]);
    const beneath = beyondBeneath(entries, { focus: games, next: null, onFocus: () => {} });
    expect(beneath({ at: '2026-11-11T13:00:00Z' }, undefined)).toBeNull();
    expect(beneath({ at: '2026-11-11T12:00:00Z' }, undefined)).not.toBeNull();
  });
});
