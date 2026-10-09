import type { ComponentProps } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { ExternalLink } from '../links';
import { CodeBlock } from './CodeBlock';

/**
 * An agent's words as a reader reads them (D76, CONV2): GitHub-flavoured Markdown — headings, lists,
 * tables, task lists, links, inline code — with fenced code as a {@link CodeBlock}.
 *
 * @remarks
 * **Safe by default** (D76 §5). Raw HTML in the text is shown as text, never parsed: the renderer is
 * not given the plugin that would, so an agent cannot put markup into the page.
 *
 * **A link leaves the application**, through `ExternalLink`: in the system's browser, or in Daoris's
 * where the person chose that (BRW7). A plain link would navigate the whole window away, which is the
 * drop-a-file failure in another form (platform language §4). **An image is never fetched** — it is
 * a link to what it names, opened only on purpose.
 *
 * **Content, not chrome** (translation-parity): the text is the agent's own, in whatever language it
 * wrote, and nothing here translates it.
 *
 * **A line the agent ended stays ended** (UX5 U4). Markdown makes a single newline a space, and an
 * agent's one-item-per-line answer drew as one paragraph on the window. The agent wrote for a
 * terminal, where a newline is a newline, as chat surfaces generally read it.
 *
 * **A word wider than its line breaks inside it** (ASKHIST1b): a URL the agent says again is one word, and it ran past Ask
 * Daoris's dock. `break-word` rather than `anywhere`, since it leaves each word's width in what a table measures, so a
 * table keeps its columns and scrolls in its own box as before.
 */
export function Markdown({ text }: { text: string }) {
  return (
    <div className="markdown text-body leading-relaxed text-ink wrap-break-word [&>*:first-child]:mt-0 [&>*:last-child]:mb-0">
      <ReactMarkdown remarkPlugins={[remarkGfm, keepLineBreaks]} components={COMPONENTS}>
        {text}
      </ReactMarkdown>
    </div>
  );
}

/** The part of a Markdown syntax tree the line rule reads: a text's value, anything's children. */
type MdNode = { type: string; value?: string; children?: MdNode[] };

/**
 * Every newline left inside prose becomes a line break. Only a `text` node holds one: code, inline
 * code and a blank line between paragraphs are other nodes, so each keeps its own meaning.
 */
function keepLineBreaks() {
  const walk = (node: MdNode) => {
    if (!node.children) return;
    node.children = node.children.flatMap((child): MdNode[] => {
      if (child.type !== 'text' || !child.value?.includes('\n')) {
        walk(child);
        return [child];
      }
      return child.value.split(/\r?\n/).flatMap((part, index): MdNode[] => [
        ...(index > 0 ? [{ type: 'break' }] : []),
        ...(part ? [{ type: 'text', value: part }] : []),
      ]);
    });
  };
  return walk;
}

type Components = ComponentProps<typeof ReactMarkdown>['components'];

const COMPONENTS: Components = {
  a: ({ href, children }) => (
    <ExternalLink href={href} className="text-accent underline underline-offset-2">
      {children}
    </ExternalLink>
  ),
  // 🔴 Never loaded (REV3). A rendered image is a request the page makes with no click, so whatever an
  // agent wrote into its URL would leave the machine even where its harness's own network tools are
  // refused (D47 §4, D52). Raw HTML was already text; a Markdown image is not raw HTML. It is a link.
  img: ({ src, alt }) => (
    <ExternalLink href={typeof src === 'string' ? src : undefined} className="text-accent underline underline-offset-2">
      {`🖼 ${alt || (typeof src === 'string' ? src : '')}`}
    </ExternalLink>
  ),
  // Fenced code is a block with a language class; inline code is neither.
  code: ({ className, children }) => {
    const language = /language-([\w+-]+)/.exec(className ?? '')?.[1];
    const text = String(children ?? '');
    if (language || text.includes('\n')) {
      return <CodeBlock code={text.replace(/\n$/, '')} language={language} />;
    }
    return <code className="rounded-[4px] bg-accent-soft px-1 py-px font-mono text-small">{children}</code>;
  },
  // The block already draws its own frame; the renderer's <pre> would draw a second.
  pre: ({ children }) => <>{children}</>,
  p: ({ children }) => <p className="my-2">{children}</p>,
  ul: ({ children }) => <ul className="my-2 list-disc pl-5">{children}</ul>,
  ol: ({ children }) => <ol className="my-2 list-decimal pl-5">{children}</ol>,
  li: ({ children }) => <li className="my-0.5">{children}</li>,
  h1: ({ children }) => <h3 className="mb-1.5 mt-3 text-title font-semibold">{children}</h3>,
  h2: ({ children }) => <h4 className="mb-1.5 mt-3 text-body font-semibold">{children}</h4>,
  h3: ({ children }) => <h5 className="mb-1 mt-2.5 text-body font-semibold">{children}</h5>,
  blockquote: ({ children }) => (
    <blockquote className="my-2 border-l-2 border-line-strong pl-3 text-ink-soft">{children}</blockquote>
  ),
  table: ({ children }) => (
    <div className="my-2 overflow-x-auto">
      <table className="border-collapse text-small">{children}</table>
    </div>
  ),
  th: ({ children }) => <th className="border border-line px-2 py-1 text-left font-semibold">{children}</th>,
  td: ({ children }) => <td className="border border-line px-2 py-1 align-top">{children}</td>,
  hr: () => <hr className="my-3 border-0 border-t border-line" />,
};
