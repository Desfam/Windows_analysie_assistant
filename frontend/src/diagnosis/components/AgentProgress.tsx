import { Check, CircleDashed, Loader2, X } from 'lucide-react'
import type { AgentStatus } from '../types'

const steps = [
  { key: 'understanding', title: 'Problem wird analysiert' },
  { key: 'planning', title: 'Nächsten Diagnoseschritt bestimmen' },
  { key: 'executing', title: 'Diagnoseaktion ausführen' },
  { key: 'evaluating', title: 'Ergebnisse auswerten' }
]

interface AgentProgressProps {
  status: AgentStatus | null
  active: boolean
}

export function AgentProgress({ status, active }: AgentProgressProps) {
  const currentIndex = status ? Math.max(0, steps.findIndex((step) => step.key === status.phase)) : 0
  const terminal = !active && status && ['completed', 'failed', 'cancelled', 'timeout'].includes(status.phase)

  return (
    <div className="border-b border-white/[0.06] px-5 py-3">
      <div className="mb-2 flex items-center justify-between text-[11px] uppercase tracking-[0.12em] text-slate-500">
        <span>Aktueller Diagnoseablauf</span>
        {status && <span className="normal-case tracking-normal text-slate-500">{status.title}</span>}
      </div>
      <div className="space-y-1.5">
        {steps.map((step, index) => {
          const isCurrent = active && status?.phase === step.key
          const isDone = index < currentIndex || (terminal && index <= currentIndex)
          const isFailed = terminal && status?.phase === 'failed' && index === currentIndex
          return (
            <div key={step.key} className={`flex items-center gap-2 rounded-md border px-2.5 py-1.5 text-xs ${
              isFailed ? 'border-red-500/35 bg-red-500/[0.08] text-red-200' :
              isCurrent ? 'border-cyan-400/45 bg-cyan-400/[0.08] text-cyan-100' :
              isDone ? 'border-emerald-400/20 text-emerald-200' : 'border-dashed border-white/[0.08] text-slate-500'
            }`}>
              {isFailed ? <X className="h-3.5 w-3.5" /> : isDone ? <Check className="h-3.5 w-3.5" /> : isCurrent ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <CircleDashed className="h-3.5 w-3.5" />}
              <span>{step.title}</span>
              <span className="ml-auto text-[10px] opacity-70">{isCurrent ? 'Wird ausgeführt' : isDone ? 'Abgeschlossen' : 'Wird bestimmt'}</span>
            </div>
          )
        })}
      </div>
      {status?.description && active && <p className="mt-2 text-xs text-slate-500">{status.description}</p>}
    </div>
  )
}
