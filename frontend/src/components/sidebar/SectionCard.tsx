import { useState, type ReactNode } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { ChevronDown, type LucideIcon } from 'lucide-react'
import type { HealthStatus } from '../../types'
import { StatusIndicator } from '../common/StatusIndicator'
import { SkeletonRows } from '../common/Skeleton'

interface SectionCardProps {
  title: string
  icon: LucideIcon
  status: HealthStatus
  loading?: boolean
  failed?: boolean
  animate?: boolean
  children: ReactNode
}

export function SectionCard({
  title,
  icon: Icon,
  status,
  loading = false,
  failed = false,
  animate = true,
  children
}: SectionCardProps) {
  const [open, setOpen] = useState(true)

  return (
    <div className="rounded-2xl border border-white/[0.06] bg-base-700/70 shadow-card">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        className="flex w-full items-center gap-3 px-4 py-3 text-left"
      >
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/[0.04] text-slate-300">
          <Icon className="h-4 w-4" aria-hidden />
        </span>
        <span className="flex-1 text-sm font-semibold text-slate-100">{title}</span>
        <StatusIndicator status={status} />
        <ChevronDown
          className={`h-4 w-4 text-slate-500 transition-transform ${open ? 'rotate-180' : ''}`}
          aria-hidden
        />
      </button>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            initial={animate ? { height: 0, opacity: 0 } : false}
            animate={{ height: 'auto', opacity: 1 }}
            exit={animate ? { height: 0, opacity: 0 } : undefined}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <div className="border-t border-white/[0.05] px-4 py-3">
              {loading ? (
                <SkeletonRows />
              ) : failed ? (
                <p className="text-sm italic text-slate-500">
                  Dieser Bereich konnte nicht gelesen werden.
                </p>
              ) : (
                children
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}
