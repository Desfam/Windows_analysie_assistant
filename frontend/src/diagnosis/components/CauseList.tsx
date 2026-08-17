import type { Cause } from '../types'
import { evidenceLevelStyles } from '../lib/styles'

interface CauseListProps {
  causes: Cause[]
}

export function CauseList({ causes }: CauseListProps) {
  return (
    <div>
      <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
        Mögliche Ursachen
      </h4>
      <ul className="space-y-1.5">
        {causes.map((cause) => {
          const style = evidenceLevelStyles[cause.level]
          const Icon = style.icon
          return (
            <li
              key={cause.id}
              className="flex items-center justify-between gap-3 rounded-lg border border-white/[0.06] bg-base-800/50 px-3 py-2"
            >
              <span className="text-sm text-slate-200">{cause.title}</span>
              <span
                className={`inline-flex shrink-0 items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-medium ${style.text} ${style.bg} ${style.border}`}
              >
                <Icon className="h-3.5 w-3.5" />
                {style.label}
              </span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
