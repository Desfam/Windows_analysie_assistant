import { Bot, User } from 'lucide-react'
import { motion } from 'framer-motion'
import type { ChatMessage as ChatMessageType, DiagnosisAction } from '../types'
import { ActionCard } from './ActionCard'

interface ChatMessageProps {
  message: ChatMessageType
  animate: boolean
  onShowCommand: (action: DiagnosisAction) => void
  onSkip: (action: DiagnosisAction) => void
  onRun: (action: DiagnosisAction) => void
}

export function ChatMessage({ message, animate, onShowCommand, onSkip, onRun }: ChatMessageProps) {
  const isUser = message.role === 'user'

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
          {message.text}
        </div>
        <div className="mt-1 px-1 text-[11px] text-slate-500">{message.timestamp}</div>

        {message.action && (
          <div className="text-left">
            <ActionCard
              action={message.action}
              onShowCommand={onShowCommand}
              onSkip={onSkip}
              onRun={onRun}
            />
          </div>
        )}
      </div>
    </motion.div>
  )
}
