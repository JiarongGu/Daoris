import { beforeEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { byTool, type ToolDoor } from './tools';
import { doorsSummary } from './agents';

const door = (over: Partial<ToolDoor>): ToolDoor => ({
  door: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: '2.1.4 (Claude Code)',
  transport: 'native', ownLogin: 'unknown', ownRead: null, profiles: [], ...over,
});
const absent = (over: Partial<ToolDoor> = {}) => door({ door: 'claude-code-acp', transport: 'protocol', accountOf: 'claude-code', present: false, version: null, ...over });

// *Ways in* says what a person can do with each way in, never the wire's name.
describe('the ways in, said in a person’s words', () => {
  beforeEach(() => i18n.changeLanguage('en'));

  it('says each way in, its version, and which one driven work starts on', () => {
    const [tool] = byTool([door({}), door({ door: 'claude-code-acp', transport: 'protocol', accountOf: 'claude-code', version: '0.9.1' })]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('command line 2.1.4, for driven work · conversations 0.9.1');
  });

  it('says a way in not installed as what it is and what to do about it', () => {
    const [tool] = byTool([door({}), absent()]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('command line 2.1.4, for driven work · conversations: not installed, open to install');
    expect(doorsSummary(tool!, 'claude-code')).not.toMatch(/protocol|direct/);
  });

  it('says where a way in Daoris has no installer for is installed from', () => {
    const [tool] = byTool([door({}), absent({ installs: false })]);
    expect(doorsSummary(tool!, 'claude-code')).toContain('conversations: not installed, installed with its maker’s own tooling');
  });

  it('says it in 中文 too', () => {
    i18n.changeLanguage('zh');
    const [tool] = byTool([door({}), absent()]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('命令行 2.1.4，用于驱动的工作 · 对话：未安装，展开后可安装');
  });

  it('keeps a Chinese word apart from the number beside it', () => {
    i18n.changeLanguage('zh');
    const [tool] = byTool([door({ present: false }), door({ door: 'claude-code-acp', transport: 'protocol', accountOf: 'claude-code', version: '0.9.1' })]);
    expect(doorsSummary(tool!, 'claude-code')).toBe('命令行：未安装，展开后可安装 · 对话 0.9.1');
  });
});
