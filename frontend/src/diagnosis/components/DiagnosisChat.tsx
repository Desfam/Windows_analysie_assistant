import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { MessageSquare, Paperclip, SendHorizontal } from 'lucide-react'
import type { ChatMessage as ChatMessageType, DiagnosisAction } from '../types'
import { ChatMessage } from './ChatMessage'

interface DiagnosisChatProps {
  messages: ChatMessageType[]
  animate: boolean
  onSend: (text: string) => void
  onShowCommand: (action: DiagnosisAction) => void
  onSkip: (action: DiagnosisAction) => void
  onRun: (action: DiagnosisAction) => void
}

export function DiagnosisChat({
  messages,
  animate,
  onSend,
  onShowCommand,
  onSkip,
  onRun
}: DiagnosisChatProps) {
  const [draft, setDraft] = useState('')
  const scrollRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: animate ? 'smooth' : 'auto' })
  }, [messages, animate])

  const submit = () => {
    if (!draft.trim()) return
    onSend(draft)
    setDraft('')
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      submit()
    }
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-start gap-3 border-b border-white/[0.06] px-5 py-4">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-sky-500/15 text-sky-300">
          <MessageSquare className="h-5 w-5" />
        </span>
        <div>
          <h2 className="text-base font-semibold text-slate-100">Diagnose-Chat</h2>
          <p className="text-sm text-slate-500">Beschreibe das Problem so genau wie möglich.</p>
        </div>
      </header>

      <div ref={scrollRef} className="min-h-0 flex-1 space-y-5 overflow-y-auto px-5 py-5">
        {messages.map((message) => (
          <ChatMessage
            key={message.id}
            message={message}
            animate={animate}
            onShowCommand={onShowCommand}
            onSkip={onSkip}
            onRun={onRun}
          />
        ))}
      </div>

      <div className="border-t border-white/[0.06] p-4">
        <div className="flex items-end gap-2 rounded-xl border border-white/[0.08] bg-base-800/70 p-2 focus-within:border-blue-500/40">
          <button
            type="button"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-slate-500 hover:bg-white/[0.05] hover:text-slate-300"
            aria-label="Anhang (Platzhalter)"
          >
            <Paperclip className="h-4 w-4" />
          </button>
          <textarea
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={handleKeyDown}
            rows={1}
            placeholder="Problem oder Beobachtung beschreiben …"
            className="max-h-32 min-h-[2.25rem] flex-1 resize-none bg-transparent px-1 py-1.5 text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none"
          />
          <button
            type="button"
            onClick={submit}
            disabled={!draft.trim()}
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-white transition-colors hover:bg-blue-500 disabled:cursor-not-allowed disabled:opacity-40"
            aria-label="Nachricht senden"
          >
            <SendHorizontal className="h-4 w-4" />
          </button>
        </div>
        <p className="mt-1.5 px-1 text-[11px] text-slate-600">
          Enter sendet · Shift+Enter für neue Zeile
        </p>
      </div>
    </div>
  )
}
