import { Clock, Code2, Info, Play, Search, ShieldCheck, SkipForward } from 'lucide-react'
import type { DiagnosisAction } from '../types'
import { RiskBadge } from './RiskBadge'
import { StatusBadge } from './StatusBadge'

interface ActionCardProps {
  action: DiagnosisAction
  onShowCommand: (action: DiagnosisAction) => void
  onSkip: (action: DiagnosisAction) => void
  onRun: (action: DiagnosisAction) => void
}

export function ActionCard({ action, onShowCommand, onSkip, onRun }: ActionCardProps) {
  const isBusy = action.state === 'running'
  const isDone = action.state === 'completed'
  const isSkipped = action.state === 'skipped'
  const disabled = isBusy || isDone || isSkipped

  return (
    <div className="mt-3 rounded-xl border border-white/[0.08] bg-base-800/70 p-4">
      <div className="flex items-start gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-violet-500/15 text-violet-300">
          <Search className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-sm font-semibold text-slate-100">{action.title}</h3>
            {action.state !== 'ready' && <StatusBadge state={action.state} />}
          </div>
          <p className="mt-0.5 text-sm text-slate-400">{action.description}</p>
        </div>
      </div>

      <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-3">
        <Detail icon={<ShieldCheck className="h-4 w-4 text-emerald-400" />} label="Systemänderung">
          {action.systemImpact.label}
        </Detail>
        <Detail icon={<RiskBadge risk={action.risk} />} label="Risiko">
          {action.risk}
        </Detail>
        <Detail icon={<Clock className="h-4 w-4 text-slate-400" />} label="Geschätzte Dauer">
          {action.estimatedDuration}
        </Detail>
      </div>

      <div className="mt-3 flex items-start gap-2 rounded-lg border border-white/[0.06] bg-white/[0.02] px-3 py-2 text-xs text-slate-400">
        <Info className="mt-0.5 h-4 w-4 shrink-0 text-sky-400" />
        <span>{action.note}</span>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => onShowCommand(action)}
          className="inline-flex items-center gap-1.5 rounded-lg border border-white/[0.08] bg-white/[0.03] px-3 py-2 text-xs font-medium text-slate-300 transition-colors hover:bg-white/[0.07]"
        >
          <Code2 className="h-4 w-4" />
          Befehl anzeigen
        </button>
        <button
          type="button"
          onClick={() => onSkip(action)}
          disabled={disabled}
          className="inline-flex items-center gap-1.5 rounded-lg border border-white/[0.08] px-3 py-2 text-xs font-medium text-slate-300 transition-colors hover:bg-white/[0.05] disabled:cursor-not-allowed disabled:opacity-40"
        >
          <SkipForward className="h-4 w-4" />
          Überspringen
        </button>
        <button
          type="button"
          onClick={() => onRun(action)}
          disabled={disabled}
          className="ml-auto inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-xs font-semibold text-white transition-colors hover:bg-blue-500 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Play className="h-4 w-4" />
          {isBusy ? 'Wird ausgeführt …' : isDone ? 'Ausgeführt' : 'Ausführen'}
        </button>
      </div>
    </div>
  )
}

function Detail({
  icon,
  label,
  children
}: {
  icon: React.ReactNode
  label: string
  children: React.ReactNode
}) {
  return (
    <div className="rounded-lg border border-white/[0.06] bg-base-900/40 px-3 py-2">
      <div className="flex items-center gap-1.5 text-[11px] text-slate-500">
        {icon}
        <span>{label}</span>
      </div>
      <div className="mt-0.5 text-xs text-slate-200">{children}</div>
    </div>
  )
}
