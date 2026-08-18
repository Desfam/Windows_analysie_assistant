import { Bot, User, AlertCircle, Loader2 } from 'lucide-react'
import { motion } from 'framer-motion'
import type { AgentStatus, ChatMessage as ChatMessageType, DiagnosisAction } from '../types'
import { ActionCard } from './ActionCard'
import { Markdown } from '../../app/components/Markdown'

function sanitizeVisibleText(text: string): string {
  let result = text
  for (const tag of ['think', 'analysis', 'reasoning']) {
    result = result.replace(new RegExp(`<${tag}\\b[^>]*>[\\s\\S]*?</${tag}\\s*>`, 'gi'), '')
    result = result.replace(new RegExp(`[\\s\\S]*?</${tag}\\s*>`, 'i'), '')
    result = result.replace(new RegExp(`<${tag}\\b[^>]*>[\\s\\S]*$`, 'i'), '')
  }
  return result.replace(/\n\s*\n\s*\n+/g, '\n\n').trim()
}

interface ChatMessageProps {
  message: ChatMessageType
  status: AgentStatus | null
  animate: boolean
  onShowCommand: (action: DiagnosisAction) => void
  onSkip: (action: DiagnosisAction) => void
}

export function ChatMessage({ message, status, animate, onShowCommand, onSkip }: ChatMessageProps) {
  const isUser = message.role === 'user'
  const visibleText = isUser ? message.text : sanitizeVisibleText(message.text)
  const showTypingDots = message.streaming && visibleText.length === 0

  return (
    <motion.div
      initial={animate ? { opacity: 0, y: 8 } : false}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25 }}
      className={`flex gap-3 ${isUser ? 'flex-row-reverse' : 'flex-row'}`}
    >
      <span
        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${
          isUser ? 'bg-blue-600 text-white' : 'bg-sky-500/15 text-sky-300'
        }`}
      >
        {isUser ? <User className="h-4 w-4" /> : <Bot className="h-4 w-4" />}
      </span>

      <div className={`max-w-[85%] ${isUser ? 'items-end text-right' : 'items-start'}`}>
        <div
          className={`rounded-2xl px-4 py-2.5 text-sm leading-relaxed ${
            isUser
              ? 'rounded-tr-sm bg-blue-600 text-white'
              : 'rounded-tl-sm border border-white/[0.06] bg-base-700/70 text-slate-200'
          }`}
        >
          {isUser ? (
            <span className="whitespace-pre-wrap">{message.text}</span>
          ) : showTypingDots ? (
            <span className="inline-flex items-center gap-1.5 text-slate-400">
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
              {status?.title ?? 'Problem wird analysiert'} …
            </span>
          ) : (
            <div className="text-left">
              <Markdown content={visibleText} />
              {message.streaming && (
                <span className="ml-0.5 inline-block h-3.5 w-1.5 animate-pulse bg-slate-400 align-middle" />
              )}
            </div>
          )}
        </div>

        <div className="mt-1 flex flex-wrap items-center gap-2 px-1 text-[11px] text-slate-500">
          <span>{message.timestamp}</span>
          {message.model && !message.streaming && <span>· {message.model}</span>}
          {message.durationMs != null && !message.streaming && (
            <span>· {(message.durationMs / 1000).toFixed(1)} s</span>
          )}
          {message.aborted && <span className="text-amber-400">· Abgebrochen</span>}
        </div>

        {message.error && (
          <div className="mt-1 inline-flex items-start gap-1.5 rounded-lg border border-red-500/20 bg-red-500/[0.06] px-2.5 py-1.5 text-left text-xs text-red-200">
            <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            <span>{message.error}</span>
          </div>
        )}

        {message.action && (
          <div className="text-left">
            <ActionCard
              action={message.action}
              onShowCommand={onShowCommand}
              onSkip={onSkip}
            />
          </div>
        )}
      </div>
    </motion.div>
  )
}
