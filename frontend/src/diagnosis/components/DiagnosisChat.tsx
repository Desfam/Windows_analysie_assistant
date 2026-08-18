import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { MessageSquare, Paperclip, SendHorizontal, Square } from 'lucide-react'
import type { AgentStatus, ChatMessage as ChatMessageType, DiagnosisAction } from '../types'
import { ChatMessage } from './ChatMessage'

interface DiagnosisChatProps {
  messages: ChatMessageType[]
  animate: boolean
  canSend: boolean
  isStreaming: boolean
  status: AgentStatus | null
  disabledReason?: string
  onSend: (text: string) => void
  onCancel: () => void
  onShowCommand: (action: DiagnosisAction) => void
  onSkip: (action: DiagnosisAction) => void
}

export function DiagnosisChat({
  messages,
  animate,
  canSend,
  isStreaming,
  status,
  disabledReason,
  onSend,
  onCancel,
  onShowCommand,
  onSkip,
}: DiagnosisChatProps) {
  const [draft, setDraft] = useState('')
  const scrollRef = useRef<HTMLDivElement>(null)
  const atBottomRef = useRef(true)

  // Automatisch nur nach unten scrollen, wenn der Benutzer bereits am Rand ist.
  useEffect(() => {
    if (atBottomRef.current) {
      scrollRef.current?.scrollTo({
        top: scrollRef.current.scrollHeight,
        behavior: animate ? 'smooth' : 'auto'
      })
    }
  }, [messages, animate])

  const handleScroll = () => {
    const el = scrollRef.current
    if (!el) return
    atBottomRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80
  }

  const submit = () => {
    if (!draft.trim() || !canSend || isStreaming) return
    onSend(draft)
    setDraft('')
    atBottomRef.current = true
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

      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="min-h-0 flex-1 space-y-5 overflow-y-auto px-5 py-5"
      >
        {messages.map((message) => (
          <ChatMessage
            key={message.id}
            message={message}
            status={status}
            animate={animate}
            onShowCommand={onShowCommand}
            onSkip={onSkip}
          />
        ))}
      </div>

      <div className="border-t border-white/[0.06] p-4">
        {!canSend && disabledReason && (
          <p className="mb-2 rounded-lg border border-amber-500/20 bg-amber-500/[0.06] px-3 py-1.5 text-xs text-amber-200">
            {disabledReason}
          </p>
        )}
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
          {isStreaming ? (
            <button
              type="button"
              onClick={onCancel}
              className="flex h-9 shrink-0 items-center gap-1.5 rounded-lg bg-red-600/90 px-3 text-white transition-colors hover:bg-red-600"
            >
              <Square className="h-3.5 w-3.5" />
              Abbrechen
            </button>
          ) : (
            <button
              type="button"
              onClick={submit}
              disabled={!draft.trim() || !canSend}
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-white transition-colors hover:bg-blue-500 disabled:cursor-not-allowed disabled:opacity-40"
              aria-label="Nachricht senden"
            >
              <SendHorizontal className="h-4 w-4" />
            </button>
          )}
        </div>
        <p className="mt-1.5 px-1 text-[11px] text-slate-600">
          Enter sendet · Shift+Enter für neue Zeile
        </p>
      </div>
    </div>
  )
}
