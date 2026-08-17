import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { Check, ChevronDown, Cpu, RefreshCw } from 'lucide-react'
import { useOllama } from '../ollama/OllamaContext'
import { useCases } from '../cases/CasesContext'
import { formatBytes, formatDate } from '../../lib/format'

interface ModelPickerProps {
  collapsed: boolean
}

export function ModelPicker({ collapsed }: ModelPickerProps) {
  const { models, modelsLoading, selectedModel, selectModel, refreshModels, phase } = useOllama()
  const { activeCase, createCase, setCaseModel } = useCases()
  const [open, setOpen] = useState(false)
  const [pendingModel, setPendingModel] = useState<string | null>(null)

  const hasConversation = activeCase.messages.some((m) => m.role === 'user')

  const choose = (name: string) => {
    setOpen(false)
    if (name === selectedModel) return
    if (hasConversation) {
      setPendingModel(name)
      return
    }
    selectModel(name)
    setCaseModel(name)
  }

  const confirmContinue = () => {
    if (!pendingModel) return
    selectModel(pendingModel)
    setCaseModel(pendingModel)
    setPendingModel(null)
  }

  const confirmNewCase = () => {
    if (!pendingModel) return
    selectModel(pendingModel)
    createCase('Neuer Diagnosefall', pendingModel)
    setPendingModel(null)
  }

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        title={collapsed ? (selectedModel ?? 'Modell wählen') : undefined}
        className={`flex w-full items-center gap-2 rounded-lg border border-white/[0.08] bg-base-800/70 px-2.5 py-2 text-left transition-colors hover:bg-base-700 ${
          collapsed ? 'justify-center' : ''
        }`}
      >
        <Cpu className="h-4 w-4 shrink-0 text-violet-300" />
        {!collapsed && (
          <>
            <span className="min-w-0 flex-1 truncate text-xs text-slate-200">
              {selectedModel ?? 'Kein Modell gewählt'}
            </span>
            <ChevronDown className="h-4 w-4 shrink-0 text-slate-500" />
          </>
        )}
      </button>

      <AnimatePresence>
        {open && (
          <>
            <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
            <motion.div
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: 8 }}
              className="absolute bottom-full z-50 mb-2 max-h-80 w-72 overflow-y-auto rounded-xl border border-white/[0.1] bg-base-800 p-1.5 shadow-2xl left-0"
            >
              <div className="flex items-center justify-between px-2 py-1.5">
                <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                  Modell wählen
                </span>
                <button
                  type="button"
                  onClick={() => void refreshModels()}
                  className="rounded-md p-1 text-slate-400 hover:bg-white/[0.06] hover:text-slate-200"
                  aria-label="Modellliste aktualisieren"
                >
                  <RefreshCw className={`h-3.5 w-3.5 ${modelsLoading ? 'animate-spin' : ''}`} />
                </button>
              </div>

              {phase === 'unreachable' ? (
                <p className="px-2 py-3 text-xs text-slate-500">Ollama ist nicht erreichbar.</p>
              ) : models.length === 0 ? (
                <p className="px-2 py-3 text-xs text-slate-500">
                  {modelsLoading ? 'Modelle werden geladen …' : 'Keine Modelle installiert.'}
                </p>
              ) : (
                models.map((model) => (
                  <button
                    key={model.name}
                    type="button"
                    onClick={() => choose(model.name)}
                    className="flex w-full items-start gap-2 rounded-lg px-2 py-2 text-left hover:bg-white/[0.05]"
                  >
                    <span className="mt-0.5 h-4 w-4 shrink-0">
                      {model.name === selectedModel && <Check className="h-4 w-4 text-emerald-400" />}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm text-slate-100">{model.name}</span>
                      <span className="block text-[11px] text-slate-500">
                        {[model.parameterSize, model.quantization, formatBytes(model.sizeBytes)]
                          .filter(Boolean)
                          .join(' · ')}
                      </span>
                      {model.modifiedAt && (
                        <span className="block text-[11px] text-slate-600">
                          Geändert: {formatDate(model.modifiedAt)}
                        </span>
                      )}
                    </span>
                  </button>
                ))
              )}
            </motion.div>
          </>
        )}
      </AnimatePresence>

      <ModelChangeDialog
        model={pendingModel}
        onContinue={confirmContinue}
        onNewCase={confirmNewCase}
        onCancel={() => setPendingModel(null)}
      />
    </div>
  )
}

function ModelChangeDialog({
  model,
  onContinue,
  onNewCase,
  onCancel
}: {
  model: string | null
  onContinue: () => void
  onNewCase: () => void
  onCancel: () => void
}) {
  return (
    <AnimatePresence>
      {model && (
        <>
          <motion.div
            className="fixed inset-0 z-[60] bg-black/50"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onCancel}
          />
          <motion.div
            className="fixed left-1/2 top-1/2 z-[61] w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-white/[0.1] bg-base-800 p-6 shadow-2xl"
            initial={{ opacity: 0, scale: 0.96 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.96 }}
          >
            <h2 className="text-base font-semibold text-slate-100">Modell wechseln</h2>
            <p className="mt-2 text-sm text-slate-400">
              Das Modell wurde geändert. Soll der bestehende Gesprächsverlauf mit dem neuen Modell
              <span className="text-slate-200"> ({model}) </span>
              fortgesetzt oder ein neuer Diagnosefall begonnen werden?
            </p>
            <div className="mt-5 flex flex-col gap-2">
              <button
                type="button"
                onClick={onContinue}
                className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-500"
              >
                Verlauf fortsetzen
              </button>
              <button
                type="button"
                onClick={onNewCase}
                className="rounded-lg border border-white/[0.1] px-4 py-2 text-sm font-medium text-slate-200 hover:bg-white/[0.05]"
              >
                Neuer Diagnosefall
              </button>
              <button
                type="button"
                onClick={onCancel}
                className="rounded-lg px-4 py-2 text-sm text-slate-400 hover:bg-white/[0.05]"
              >
                Abbrechen
              </button>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  )
}
