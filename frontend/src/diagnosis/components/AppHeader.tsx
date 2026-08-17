import { Activity, Settings } from 'lucide-react'

export function AppHeader() {
  return (
    <header className="flex items-center gap-4 border-b border-white/[0.06] bg-base-800/80 px-5 py-3 backdrop-blur">
      <div className="flex items-center gap-2.5">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-blue-500/15 text-blue-300">
          <Activity className="h-5 w-5" />
        </span>
        <span className="text-sm font-semibold text-slate-100">Windows Diagnose</span>
      </div>

      <div className="mx-auto">
        <span className="inline-flex items-center gap-2 rounded-full border border-white/[0.08] bg-base-700/60 px-3 py-1.5 text-xs text-slate-300">
          <span className="relative flex h-2 w-2">
            <span className="absolute inline-flex h-full w-full rounded-full bg-emerald-400/60" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-400" />
          </span>
          Lokaler Rechner · Verbunden
        </span>
      </div>

      <button
        type="button"
        className="inline-flex items-center gap-1.5 rounded-lg border border-white/[0.08] bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-300 transition-colors hover:bg-white/[0.07]"
      >
        <Settings className="h-4 w-4" />
        Einstellungen
      </button>
    </header>
  )
}
