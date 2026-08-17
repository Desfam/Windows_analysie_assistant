import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

interface MarkdownProps {
  content: string
}

/**
 * Rendert Modell-Antworten als sicheres Markdown. react-markdown rendert
 * standardmäßig kein rohes HTML, daher wird kein ungefiltertes HTML ausgegeben.
 */
export function Markdown({ content }: MarkdownProps) {
  return (
    <div className="space-y-2 text-sm leading-relaxed [&_a]:text-blue-300 [&_a]:underline [&_code]:rounded [&_code]:bg-black/30 [&_code]:px-1 [&_code]:py-0.5 [&_code]:text-[0.85em] [&_li]:ml-4 [&_li]:list-disc [&_ol_li]:list-decimal [&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:bg-black/40 [&_pre]:p-3 [&_strong]:text-slate-100">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ children, href }) => (
            <a href={href} target="_blank" rel="noopener noreferrer nofollow">
              {children}
            </a>
          )
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  )
}
