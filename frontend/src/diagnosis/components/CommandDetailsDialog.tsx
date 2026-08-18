import { AnimatePresence, motion } from 'framer-motion'
import { Info, TerminalSquare, X } from 'lucide-react'
import type { DiagnosisAction } from '../types'
import { RiskBadge } from './RiskBadge'

interface CommandDetailsDialogProps {
  action: DiagnosisAction | null
  onClose: () => void
}

export function CommandDetailsDialog({ action, onClose }: CommandDetailsDialogProps) {
  return (
    <AnimatePresence>
      {action && (
        <>
          <motion.div
            className="fixed inset-0 z-40 bg-black/50"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />
          <motion.div
            role="dialog"
            aria-modal="true"
            className="fixed left-1/2 top-1/2 z-50 w-full max-w-lg -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-white/[0.08] bg-base-800 p-6 shadow-2xl"
            initial={{ opacity: 0, scale: 0.96 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.96 }}
          >
            <div className="mb-4 flex items-start justify-between gap-4">
              <div className="flex items-center gap-2.5">
                <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-violet-500/15 text-violet-300">
                  <TerminalSquare className="h-5 w-5" />
                </span>
                <div>
                  <h2 className="text-base font-semibold text-slate-100">Befehl anzeigen</h2>
                  <p className="text-xs text-slate-500">{action.title}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-white/[0.06] hover:text-slate-200"
                aria-label="Schließen"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words rounded-lg border border-white/[0.06] bg-base-900/70 p-4 text-xs leading-relaxed text-slate-300">
              {action.execution ? `${action.execution.program} ${action.execution.arguments.join(' ')}` : action.command}
            </pre>

            <div className="mt-4 flex items-center gap-2">
              <RiskBadge risk={action.risk} showIcon />
              <span className="text-xs text-slate-400">{action.systemImpact.label}</span>
            </div>

            <div className="mt-4 flex items-start gap-2 rounded-lg border border-sky-500/20 bg-sky-500/[0.06] px-3 py-2 text-xs text-sky-200">
              <Info className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{action.state === 'running'
                ? 'Der Diagnosebefehl wird aktuell lokal ausgeführt.'
                : action.execution
                  ? 'Der Diagnosebefehl wurde lokal ausgeführt und hat keine Systemänderung vorgenommen.'
                  : 'Dieser freigegebene, lesende Diagnosebefehl wird lokal ausgeführt, sobald die Diagnoseaktion startet.'}</span>
            </div>
            {action.execution && (
              <div className="mt-4 space-y-3 text-xs text-slate-300">
                <p>Start: {new Date(action.execution.startedAt).toLocaleString('de-DE')} · Ende: {new Date(action.execution.completedAt).toLocaleString('de-DE')} · Dauer: {(action.execution.durationMs / 1000).toLocaleString('de-DE')} s · Exitcode: {action.execution.exitCode}</p>
                <Output label="Standardausgabe" value={action.execution.standardOutput} />
                <Output label="Fehlerausgabe" value={action.execution.standardError || action.execution.startError || ''} />
              </div>
            )}
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}

function Output({ label, value }: { label: string; value: string }) {
  return <div><p className="mb-1 text-slate-500">{label}</p><pre className="max-h-32 overflow-auto whitespace-pre-wrap break-words rounded-lg border border-white/[0.06] bg-base-900/70 p-3">{value || 'Keine Ausgabe'}</pre></div>
}
