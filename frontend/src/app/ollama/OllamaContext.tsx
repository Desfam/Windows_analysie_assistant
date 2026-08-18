import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode
} from 'react'
import { ollamaApi, type OllamaModel, type OllamaStatus } from './ollamaApi'

export type ConnectionPhase = 'checking' | 'connected' | 'unreachable'

export interface OllamaSettings {
  selectedModel: string | null
  streaming: boolean
  autoloadModel: boolean
  persistHistory: boolean
}

interface OllamaContextValue {
  phase: ConnectionPhase
  status: OllamaStatus | null
  models: OllamaModel[]
  modelsLoading: boolean
  baseUrl: string
  isLocal: boolean
  settings: OllamaSettings
  selectedModel: string | null
  refreshStatus: () => Promise<void>
  refreshModels: () => Promise<void>
  selectModel: (name: string) => void
  updateBaseUrl: (baseUrl: string) => Promise<void>
  updateSettings: (partial: Partial<OllamaSettings>) => void
}

const STORAGE_KEY = 'wda.ollama.v1'

const defaultSettings: OllamaSettings = {
  selectedModel: null,
  streaming: true,
  autoloadModel: false,
  persistHistory: true,
}

function loadSettings(): OllamaSettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaultSettings
    return { ...defaultSettings, ...(JSON.parse(raw) as Partial<OllamaSettings>) }
  } catch {
    return defaultSettings
  }
}

const OllamaContext = createContext<OllamaContextValue | null>(null)

export function OllamaProvider({ children }: { children: ReactNode }) {
  const [phase, setPhase] = useState<ConnectionPhase>('checking')
  const [status, setStatus] = useState<OllamaStatus | null>(null)
  const [models, setModels] = useState<OllamaModel[]>([])
  const [modelsLoading, setModelsLoading] = useState(false)
  const [baseUrl, setBaseUrl] = useState('http://127.0.0.1:11434')
  const [isLocal, setIsLocal] = useState(true)
  const [settings, setSettings] = useState<OllamaSettings>(loadSettings)
  const initialised = useRef(false)

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
  }, [settings])

  const refreshStatus = useCallback(async () => {
    setPhase('checking')
    try {
      const result = await ollamaApi.status()
      setStatus(result)
      setPhase(result.connected ? 'connected' : 'unreachable')
    } catch {
      setPhase('unreachable')
    }
  }, [])

  const refreshModels = useCallback(async () => {
    setModelsLoading(true)
    try {
      const result = await ollamaApi.models()
      setModels(result.models)
      // Gespeichertes Modell wiederherstellen, sonst erstes verfügbares anbieten.
      setSettings((prev) => {
        if (result.models.length === 0) {
          return { ...prev, selectedModel: null }
        }
        const stillExists = prev.selectedModel
          ? result.models.some((m) => m.name === prev.selectedModel)
          : false
        return stillExists ? prev : { ...prev, selectedModel: result.models[0].name }
      })
    } catch {
      setModels([])
    } finally {
      setModelsLoading(false)
    }
  }, [])

  const updateBaseUrl = useCallback(
    async (nextBaseUrl: string) => {
      const config = await ollamaApi.setConfig(nextBaseUrl)
      setBaseUrl(config.baseUrl)
      setIsLocal(config.isLocal)
      await refreshStatus()
      await refreshModels()
    },
    [refreshStatus, refreshModels]
  )

  const selectModel = useCallback((name: string) => {
    setSettings((prev) => ({ ...prev, selectedModel: name }))
  }, [])

  const updateSettings = useCallback((partial: Partial<OllamaSettings>) => {
    setSettings((prev) => ({ ...prev, ...partial }))
  }, [])

  useEffect(() => {
    if (initialised.current) return
    initialised.current = true

    void (async () => {
      try {
        const config = await ollamaApi.getConfig()
        setBaseUrl(config.baseUrl)
        setIsLocal(config.isLocal)
      } catch {
        // Backend-Konfiguration nicht erreichbar – Standard beibehalten.
      }
      await refreshStatus()
      await refreshModels()
    })()
  }, [refreshStatus, refreshModels])

  const value = useMemo<OllamaContextValue>(
    () => ({
      phase,
      status,
      models,
      modelsLoading,
      baseUrl,
      isLocal,
      settings,
      selectedModel: settings.selectedModel,
      refreshStatus,
      refreshModels,
      selectModel,
      updateBaseUrl,
      updateSettings
    }),
    [
      phase,
      status,
      models,
      modelsLoading,
      baseUrl,
      isLocal,
      settings,
      refreshStatus,
      refreshModels,
      selectModel,
      updateBaseUrl,
      updateSettings
    ]
  )

  return <OllamaContext.Provider value={value}>{children}</OllamaContext.Provider>
}

export function useOllama(): OllamaContextValue {
  const context = useContext(OllamaContext)
  if (!context) {
    throw new Error('useOllama muss innerhalb von OllamaProvider verwendet werden.')
  }
  return context
}
