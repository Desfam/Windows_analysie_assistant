import { motion } from 'framer-motion'
import { Activity, RefreshCw, Settings, ShieldCheck } from 'lucide-react'
import { formatDateTime } from '../lib/format'

interface StatusBarProps {
  machineName: string
  lastUpdated: Date | null
  loading: boolean
  hasError: boolean
  onRefresh: () => void
  onOpenSettings: () => void
}

export function StatusBar({
  machineName,
  lastUpdated,
  loading,
  hasError,
  onRefresh,
  onOpenSettings
}: StatusBarProps) {
  const state = hasError
    ? { label: 'Fehler bei der Erfassung', tone: 'text-red-300', dot: 'bg-red-400' }
    : loading
      ? { label: 'Daten werden erfasst…', tone: 'text-indigo-300', dot: 'bg-indigo-400' }
      : { label: 'Erfassung aktiv', tone: 'text-emerald-300', dot: 'bg-emerald-400' }

  return (
    <header className="flex flex-wrap items-center gap-4 border-b border-white/[0.06] bg-base-800/80 px-6 py-3 backdrop-blur">
      <div className="flex items-center gap-2.5">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-indigo-500/15 text-indigo-300">
          <Activity className="h-5 w-5" />
        </span>
        <div>
          <h1 className="text-sm font-semibold leading-tight text-slate-100">Windows Diagnose Assistent</h1>
          <p className="text-xs text-slate-500">{machineName}</p>
        </div>
      </div>

      <div className="flex items-center gap-2 text-xs">
        <span className={`inline-flex items-center gap-1.5 ${state.tone}`}>
          <span className={`relative flex h-2 w-2`}>
            {loading && (
              <motion.span
                className={`absolute inline-flex h-full w-full rounded-full ${state.dot} opacity-60`}
                animate={{ scale: [1, 2], opacity: [0.6, 0] }}
                transition={{ duration: 1.2, repeat: Infinity }}
              />
            )}
            <span className={`relative inline-flex h-2 w-2 rounded-full ${state.dot}`} />
          </span>
          {state.label}
        </span>
      </div>

      <div className="ml-auto flex items-center gap-3">
        <span className="hidden text-xs text-slate-500 sm:block">
          Zuletzt aktualisiert: {lastUpdated ? formatDateTime(lastUpdated) : '—'}
        </span>

        <span
          className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-500/20 bg-emerald-500/[0.08] px-2.5 py-1.5 text-xs text-emerald-300"
          title="Der Dienst ist ausschließlich lokal unter 127.0.0.1 erreichbar."
        >
          <ShieldCheck className="h-3.5 w-3.5" />
          Lokale Verbindung
        </span>

        <button
          type="button"
          onClick={onRefresh}
          className="inline-flex items-center gap-1.5 rounded-lg border border-indigo-500/30 bg-indigo-500/10 px-3 py-1.5 text-xs font-medium text-indigo-200 transition-colors hover:bg-indigo-500/20"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${loading ? 'animate-spin' : ''}`} />
          Aktualisieren
        </button>

        <button
          type="button"
          onClick={onOpenSettings}
          className="inline-flex items-center justify-center rounded-lg border border-white/[0.08] bg-white/[0.03] p-2 text-slate-300 transition-colors hover:bg-white/[0.07]"
          aria-label="Einstellungen"
        >
          <Settings className="h-4 w-4" />
        </button>
      </div>
    </header>
  )
}
