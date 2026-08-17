import { useState } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  Plug,
  RefreshCw,
  Settings as SettingsIcon,
  ShieldAlert
} from 'lucide-react'
import { useOllama } from '../ollama/OllamaContext'
import { formatBytes } from '../../lib/format'

export function SettingsPage() {
  const {
    phase,
    status,
    baseUrl,
    isLocal,
    models,
    modelsLoading,
    selectedModel,
    settings,
    selectModel,
    refreshModels,
    updateBaseUrl,
    updateSettings
  } = useOllama()

  const [draftUrl, setDraftUrl] = useState(baseUrl)
  const [testing, setTesting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const testConnection = async () => {
    setTesting(true)
    setError(null)
    setSaved(false)
    try {
      await updateBaseUrl(draftUrl)
      setSaved(true)
    } catch (caught) {
      setError((caught as Error).message)
    } finally {
      setTesting(false)
    }
  }

  return (
    <div className="flex h-full flex-col overflow-hidden">
      <header className="flex items-center gap-2.5 border-b border-white/[0.06] px-6 py-4">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-slate-500/15 text-slate-300">
          <SettingsIcon className="h-5 w-5" />
        </span>
        <div>
          <h1 className="text-base font-semibold text-slate-100">Einstellungen</h1>
          <p className="text-sm text-slate-500">Lokale Ollama-Anbindung und Chat-Verhalten</p>
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto p-6">
        <div className="mx-auto max-w-2xl space-y-6">
          <Section title="Ollama-Verbindung" icon={<Plug className="h-4 w-4 text-blue-300" />}>
            <label className="block text-sm text-slate-300">Basisadresse</label>
            <div className="mt-1.5 flex gap-2">
              <input
                value={draftUrl}
                onChange={(e) => setDraftUrl(e.target.value)}
                placeholder="http://127.0.0.1:11434"
                className="flex-1 rounded-lg border border-white/[0.08] bg-base-800 px-3 py-2 text-sm text-slate-100 focus:border-blue-500/40 focus:outline-none"
              />
              <button
                type="button"
                onClick={() => void testConnection()}
                disabled={testing}
                className="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-500 disabled:opacity-50"
              >
                <RefreshCw className={`h-4 w-4 ${testing ? 'animate-spin' : ''}`} />
                Verbindung testen
              </button>
            </div>

            {error && (
              <p className="mt-2 flex items-center gap-1.5 text-xs text-red-300">
                <AlertTriangle className="h-3.5 w-3.5" />
                {error}
              </p>
            )}
            {saved && !error && (
              <p className="mt-2 flex items-center gap-1.5 text-xs text-emerald-300">
                <CheckCircle2 className="h-3.5 w-3.5" />
                Adresse gespeichert.
              </p>
            )}

            <div className="mt-3 flex flex-wrap items-center gap-3 text-xs">
              <span className="text-slate-500">
                Status:{' '}
                <span
                  className={
                    phase === 'connected'
                      ? 'text-emerald-300'
                      : phase === 'checking'
                        ? 'text-amber-300'
                        : 'text-red-300'
                  }
                >
                  {phase === 'connected' ? 'verbunden' : phase === 'checking' ? 'wird geprüft' : 'nicht erreichbar'}
                </span>
              </span>
              {status?.version && <span className="text-slate-500">Version: {status.version}</span>}
            </div>

            {!isLocal && (
              <div className="mt-3 flex items-start gap-2 rounded-lg border border-orange-500/25 bg-orange-500/[0.08] px-3 py-2 text-xs text-orange-200">
                <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
                <span>
                  Es ist eine nicht-lokale Adresse eingestellt. Stelle sicher, dass du diesem Ziel im
                  privaten Netzwerk vertraust. Öffentliche Ziele sind grundsätzlich gesperrt.
                </span>
              </div>
            )}
          </Section>

          <Section title="Modelle" icon={<RefreshCw className="h-4 w-4 text-violet-300" />}>
            <div className="flex items-center justify-between">
              <label className="text-sm text-slate-300">Standardmodell</label>
              <button
                type="button"
                onClick={() => void refreshModels()}
                className="inline-flex items-center gap-1.5 text-xs text-slate-400 hover:text-slate-200"
              >
                <RefreshCw className={`h-3.5 w-3.5 ${modelsLoading ? 'animate-spin' : ''}`} />
                Liste aktualisieren
              </button>
            </div>

            {phase === 'unreachable' ? (
              <p className="mt-2 text-sm text-slate-500">Ollama ist nicht erreichbar.</p>
            ) : models.length === 0 ? (
              <p className="mt-2 text-sm text-slate-500">
                {modelsLoading
                  ? 'Modelle werden geladen …'
                  : 'Ollama ist verbunden, aber es wurden keine installierten Modelle gefunden.'}
              </p>
            ) : (
              <select
                value={selectedModel ?? ''}
                onChange={(e) => selectModel(e.target.value)}
                className="mt-1.5 w-full rounded-lg border border-white/[0.08] bg-base-800 px-3 py-2 text-sm text-slate-100 focus:border-blue-500/40 focus:outline-none"
              >
                {models.map((model) => (
                  <option key={model.name} value={model.name}>
                    {model.name}
                    {model.parameterSize ? ` · ${model.parameterSize}` : ''}
                    {` · ${formatBytes(model.sizeBytes)}`}
                  </option>
                ))}
              </select>
            )}
          </Section>

          <Section title="Chat-Verhalten" icon={<SettingsIcon className="h-4 w-4 text-slate-300" />}>
            <Toggle
              label="Streaming aktiv"
              description="Antworten während der Generierung anzeigen"
              checked={settings.streaming}
              onChange={(v) => updateSettings({ streaming: v })}
            />
            <Toggle
              label="Modell beim App-Start vorladen"
              description="Optionales Vorladen des Standardmodells"
              checked={settings.autoloadModel}
              onChange={(v) => updateSettings({ autoloadModel: v })}
            />
            <Toggle
              label="Gesprächsverlauf lokal speichern"
              description="Verlauf im Browser dieses Rechners behalten"
              checked={settings.persistHistory}
              onChange={(v) => updateSettings({ persistHistory: v })}
            />
            <Toggle
              label="Demo-Daten für neue Fälle verwenden"
              description="Neue Fälle starten mit dem Beispiel „System-Freezes“ statt leer. Standardmäßig aus."
              checked={settings.useDemoDataForNewCases}
              onChange={(v) => updateSettings({ useDemoDataForNewCases: v })}
            />
          </Section>
        </div>
      </div>
    </div>
  )
}

function Section({
  title,
  icon,
  children
}: {
  title: string
  icon: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <section className="rounded-2xl border border-white/[0.06] bg-base-800/40 p-5">
      <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-slate-100">
        {icon}
        {title}
      </h2>
      {children}
    </section>
  )
}

function Toggle({
  label,
  description,
  checked,
  onChange
}: {
  label: string
  description: string
  checked: boolean
  onChange: (value: boolean) => void
}) {
  return (
    <label className="flex items-center justify-between gap-3 py-2">
      <span>
        <span className="block text-sm text-slate-200">{label}</span>
        <span className="block text-xs text-slate-500">{description}</span>
      </span>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
          checked ? 'bg-blue-500/70' : 'bg-white/[0.12]'
        }`}
      >
        <span
          className={`absolute top-0.5 h-5 w-5 rounded-full bg-white transition-transform ${
            checked ? 'translate-x-5' : 'translate-x-0.5'
          }`}
        />
      </button>
    </label>
  )
}
