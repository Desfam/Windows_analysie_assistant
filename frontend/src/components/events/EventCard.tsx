import { motion } from 'framer-motion'
import { ChevronRight, Repeat, Sparkles } from 'lucide-react'
import type { EventItem } from '../../types'
import { severityStyles } from '../../lib/status'
import { formatRelativeDay, formatTime } from '../../lib/format'

interface EventCardProps {
  event: EventItem
  isNew: boolean
  animate: boolean
  onSelect: (event: EventItem) => void
  onInvestigate?: (event: EventItem) => void
}

export function EventCard({ event, isNew, animate, onSelect, onInvestigate }: EventCardProps) {
  const style = severityStyles[event.severity]
  const isCritical = event.severity === 'Critical'

  const glow =
    animate && isNew
      ? isCritical
        ? { boxShadow: ['0 0 0 0 rgba(239,68,68,0)', '0 0 0 6px rgba(239,68,68,0.35)', '0 0 0 0 rgba(239,68,68,0)'] }
        : { boxShadow: ['0 0 0 0 rgba(249,115,22,0)', '0 0 0 6px rgba(249,115,22,0.3)', '0 0 0 0 rgba(249,115,22,0)'] }
      : undefined

  return (
    <motion.div
      role="button"
      tabIndex={0}
      layout={animate}
      onClick={() => onSelect(event)}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect(event)
        }
      }}
      initial={animate && isNew ? { opacity: 0, y: -16 } : false}
      animate={{ opacity: 1, y: 0, ...(glow ?? {}) }}
      transition={{ duration: 0.35, boxShadow: { duration: 1.1, repeat: isCritical ? 1 : 0 } }}
      className={`group w-full cursor-pointer rounded-xl border ${style.border} bg-base-700/70 p-4 text-left transition-colors hover:bg-base-600/70`}
    >
      <div className="flex items-center gap-2 text-xs">
        <span className={`inline-flex items-center gap-1.5 rounded-md px-2 py-0.5 font-semibold uppercase tracking-wide ${style.badge}`}>
          <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} />
          {style.label}
        </span>
        <span className="text-slate-500">
          {formatRelativeDay(event.lastSeen)}, {formatTime(event.lastSeen)}
        </span>
        {event.count > 1 && (
          <span className="ml-auto inline-flex items-center gap-1 text-slate-400">
            <Repeat className="h-3.5 w-3.5" />
            {event.count}×
          </span>
        )}
      </div>

      <h3 className="mt-2 text-[15px] font-semibold text-slate-100">{event.title}</h3>

      <p className="mt-1.5 line-clamp-2 text-sm text-slate-400">{event.summary}</p>

      <div className="mt-3 flex items-center justify-between gap-2 text-xs text-slate-500">
        <span className="truncate">
          {event.providerName} · ID {event.eventId} · {event.logName}
        </span>
        <div className="flex shrink-0 items-center gap-3">
          {onInvestigate && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation()
                onInvestigate(event)
              }}
              className="inline-flex items-center gap-1 rounded-md border border-violet-500/30 bg-violet-500/10 px-2 py-1 font-medium text-violet-200 transition-colors hover:bg-violet-500/20"
            >
              <Sparkles className="h-3.5 w-3.5" />
              In KI-Diagnose untersuchen
            </button>
          )}
          <span className="inline-flex items-center gap-1 text-indigo-300 opacity-0 transition-opacity group-hover:opacity-100">
            Details <ChevronRight className="h-3.5 w-3.5" />
          </span>
        </div>
      </div>

      {event.count > 1 && (
        <p className="mt-2 text-xs text-slate-500">
          {event.count}× aufgetreten · zuerst {formatTime(event.firstSeen)}, zuletzt {formatTime(event.lastSeen)}
        </p>
      )}
    </motion.div>
  )
}

