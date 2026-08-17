import { ShieldAlert, X } from 'lucide-react'
import type { DiagnosisNode } from '../types'
import { kindStyles, riskStyles } from '../lib/styles'
import { StatusBadge } from './StatusBadge'
import { RiskBadge } from './RiskBadge'

interface NodeDetailPanelProps {
  node: DiagnosisNode
  onClose: () => void
  onApprove?: (id: string) => void
}

export function NodeDetailPanel({ node, onClose, onApprove }: NodeDetailPanelProps) {
  const data = node.data
  const kind = kindStyles[data.kind]
  const Icon = kind.icon
  const needsApproval =
    (data.kind === 'repair' && data.requiresApproval) || data.state === 'waitingForApproval'

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <span className={`flex h-9 w-9 items-center justify-center rounded-lg bg-white/[0.05] ${kind.accent}`}>
            <Icon className="h-5 w-5" />
          </span>
          <div>
            <p className={`text-[11px] font-medium uppercase tracking-wide ${kind.accent}`}>
              {kind.label}
            </p>
            <h3 className="text-sm font-semibold text-slate-100">{data.title}</h3>
          </div>
        </div>
        <button
          type="button"
          onClick={onClose}
          className="rounded-lg p-1.5 text-slate-400 hover:bg-white/[0.06] hover:text-slate-200"
          aria-label="Detail schließen"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <StatusBadge state={data.state} size="md" />
        <RiskBadge risk={data.risk} showIcon />
        <span className="text-xs text-slate-400">{data.systemImpact.label}</span>
        {data.estimatedDuration && (
          <span className="text-xs text-slate-500">· {data.estimatedDuration}</span>
        )}
      </div>

      <div className="mt-3 min-h-0 flex-1 space-y-3 overflow-y-auto pr-1 text-sm">
        {data.reason && <Field label="Begründung">{data.reason}</Field>}
        {data.condition && <Field label="Bedingung">{data.condition}</Field>}
        {data.result && <Field label="Ergebnis">{data.result}</Field>}

        <Field label="Systemauswirkung">{riskStyles[data.risk].description}</Field>

        {(data.startedAt || data.finishedAt) && (
          <div className="flex gap-6">
            {data.startedAt && <Field label="Start">{data.startedAt}</Field>}
            {data.finishedAt && <Field label="Ende">{data.finishedAt}</Field>}
          </div>
        )}

        {data.evidence && data.evidence.length > 0 && (
          <Field label="Gefundene Belege">
            <ul className="space-y-1">
              {data.evidence.map((item) => (
                <li key={item.id} className="text-slate-300">
                  Ereignis {item.eventId} · {item.source} — {item.summary}
                </li>
              ))}
            </ul>
          </Field>
        )}

        {data.nextSteps && data.nextSteps.length > 0 && (
          <Field label="Mögliche nächste Schritte">
            <ul className="list-inside list-disc text-slate-300">
              {data.nextSteps.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ul>
          </Field>
        )}

        {data.demoCommand && (
          <Field label="Verwendeter Demo-Befehl">
            <pre className="mt-1 overflow-auto whitespace-pre-wrap break-words rounded-lg border border-white/[0.06] bg-base-900/70 p-3 text-xs text-slate-300">
              {data.demoCommand}
            </pre>
          </Field>
        )}
      </div>

      {needsApproval && (
        <div className="mt-3 rounded-lg border border-orange-500/30 bg-orange-500/[0.08] p-3">
          <div className="flex items-center gap-2 text-xs text-orange-200">
            <ShieldAlert className="h-4 w-4" />
            Dieser Schritt erfordert eine ausdrückliche Bestätigung, bevor er ausgeführt wird.
          </div>
          <button
            type="button"
            onClick={() => onApprove?.(node.id)}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-orange-500/90 px-3 py-1.5 text-xs font-semibold text-white hover:bg-orange-500"
          >
            Ausführung bestätigen
          </button>
        </div>
      )}
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{label}</p>
      <div className="mt-0.5 text-slate-300">{children}</div>
    </div>
  )
}
