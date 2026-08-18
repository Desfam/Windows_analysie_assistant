import { useNavigate } from 'react-router-dom'
import { FolderKanban, MessageSquare, Network, Plus } from 'lucide-react'
import { useCases } from '../cases/CasesContext'
import { useOllama } from '../ollama/OllamaContext'
import type { CaseStatus } from '../cases/casesTypes'

const statusMeta: Record<CaseStatus, { label: string; className: string }> = {
  open: { label: 'Offen', className: 'text-slate-300 bg-slate-500/10 border-white/10' },
  running: { label: 'Untersuchung läuft', className: 'text-blue-300 bg-blue-500/10 border-blue-500/20' },
  waiting: { label: 'Wartet', className: 'text-orange-300 bg-orange-500/10 border-orange-500/20' },
  resolved: { label: 'Gelöst', className: 'text-emerald-300 bg-emerald-500/10 border-emerald-500/20' },
  closed: { label: 'Geschlossen', className: 'text-slate-400 bg-slate-500/5 border-white/10' }
}

export function CasesPage() {
  const { cases, activeId, selectCase, createCase } = useCases()
  const { selectedModel } = useOllama()
  const navigate = useNavigate()

  const openCase = (id: string) => {
    selectCase(id)
    navigate('/diagnosis')
  }

  const startNewCase = () => {
    createCase('Neuer Diagnosefall', selectedModel)
    navigate('/diagnosis')
  }

  return (
    <div className="flex h-full flex-col overflow-hidden">
      <header className="flex items-center justify-between border-b border-white/[0.06] px-6 py-4">
        <div className="flex items-center gap-2.5">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-violet-500/15 text-violet-300">
            <FolderKanban className="h-5 w-5" />
          </span>
          <div>
            <h1 className="text-base font-semibold text-slate-100">Diagnosefälle</h1>
            <p className="text-sm text-slate-500">Laufende und abgeschlossene Untersuchungen</p>
          </div>
        </div>
        <button
          type="button"
          onClick={startNewCase}
          className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-500"
        >
          <Plus className="h-4 w-4" />
          Neuer Fall
        </button>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto p-6">
        {cases.length === 0 ? (
          <EmptyState onCreate={startNewCase} />
        ) : (
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
            {cases.map((item) => {
              const meta = statusMeta[item.status]
              const isActive = item.id === activeId
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => openCase(item.id)}
                  className={`rounded-2xl border p-4 text-left transition-colors hover:bg-base-700/70 ${
                    isActive ? 'border-blue-500/40 bg-base-700/60' : 'border-white/[0.06] bg-base-800/40'
                  }`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <h3 className="text-sm font-semibold text-slate-100">{item.title}</h3>
                    <span className={`shrink-0 rounded-md border px-2 py-0.5 text-[11px] font-medium ${meta.className}`}>
                      {meta.label}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-slate-500">
                    Modell: {item.modelName ?? 'nicht gewählt'}
                  </p>
                  <div className="mt-3 flex items-center gap-4 text-xs text-slate-400">
                    <span className="inline-flex items-center gap-1">
                      <MessageSquare className="h-3.5 w-3.5" />
                      {item.messages.filter((m) => m.role === 'user').length} Nachrichten
                    </span>
                    <span className="inline-flex items-center gap-1">
                      <Network className="h-3.5 w-3.5" />
                      {item.nodes.length} Knoten
                    </span>
                  </div>
                </button>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}

function EmptyState({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="flex h-full flex-col items-center justify-center text-center">
      <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-full bg-white/[0.04]">
        <FolderKanban className="h-7 w-7 text-slate-400" />
      </div>
      <h2 className="text-base font-medium text-slate-200">Noch keine Diagnosefälle</h2>
      <p className="mt-1 max-w-sm text-sm text-slate-500">
        Starte eine neue Untersuchung oder übergib ein Ereignis aus der Systemübersicht an die KI-Diagnose.
      </p>
      <button
        type="button"
        onClick={onCreate}
        className="mt-4 inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-500"
      >
        <Plus className="h-4 w-4" />
        Diagnosefall anlegen
      </button>
    </div>
  )
}
