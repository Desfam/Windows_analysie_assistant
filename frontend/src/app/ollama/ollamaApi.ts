import { authHeaders } from '../session'

export interface OllamaStatus {
  connected: boolean
  version: string | null
  baseUrl: string
  error: string | null
  checkedAt: string
}

export interface OllamaModel {
  name: string
  family: string | null
  parameterSize: string | null
  quantization: string | null
  sizeBytes: number
  modifiedAt: string | null
}

export interface OllamaModelsResponse {
  models: OllamaModel[]
  connected: boolean
  error: string | null
}

export interface OllamaConfig {
  baseUrl: string
  isLocal: boolean
  allowPrivateNetwork: boolean
}

export interface OllamaChatMessage {
  role: 'user' | 'assistant' | 'system'
  content: string
}

export interface OllamaCaseContext {
  computerName?: string
  selectedEvents?: string[]
  currentEvidence?: string[]
}

export interface OllamaChatRequest {
  model: string
  messages: OllamaChatMessage[]
  caseContext?: OllamaCaseContext
}

export interface ChatStreamChunk {
  type: 'delta' | 'done' | 'error'
  content?: string
  message?: string
  durationMs?: number
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, { headers: authHeaders(), signal })
  if (!response.ok) {
    throw new Error(`Anfrage fehlgeschlagen (${response.status}).`)
  }
  return (await response.json()) as T
}

export const ollamaApi = {
  status: (signal?: AbortSignal) => getJson<OllamaStatus>('/api/ollama/status', signal),
  models: (signal?: AbortSignal) => getJson<OllamaModelsResponse>('/api/ollama/models', signal),
  getConfig: (signal?: AbortSignal) => getJson<OllamaConfig>('/api/ollama/config', signal),

  async setConfig(baseUrl: string): Promise<OllamaConfig> {
    const response = await fetch('/api/ollama/config', {
      method: 'PUT',
      headers: authHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ baseUrl })
    })
    if (!response.ok) {
      const body = (await response.json().catch(() => null)) as { error?: string } | null
      throw new Error(body?.error ?? 'Die Adresse konnte nicht gespeichert werden.')
    }
    return (await response.json()) as OllamaConfig
  }
}

export interface AgentGraphNode {
  id: string
  kind: string
  title: string
  description: string
  state: string
  riskLevel: string
  changesSystem: boolean
}

export interface AgentGraphNodePatch {
  id: string
  state: string
  result?: string | null
  error?: string | null
}

export interface AgentEvidence {
  id: string
  eventId?: number | null
  provider?: string | null
  summary: string
  timestamp?: string | null
}

/** Ein eindeutig typisiertes Ereignis der serverseitigen Agenten-Orchestrierung. */
export interface AgentEvent {
  type:
    | 'assistant.delta'
    | 'assistant.completed'
    | 'action.proposed'
    | 'graph.nodeAdded'
    | 'graph.nodeUpdated'
    | 'action.started'
    | 'action.completed'
    | 'evidence.added'
    | 'error'
  content?: string
  actionId?: string
  parameters?: unknown
  reason?: string
  node?: AgentGraphNode
  nodePatch?: AgentGraphNodePatch
  executionId?: string
  result?: unknown
  evidence?: AgentEvidence
  messageId?: string
  durationMs?: number
  code?: string
  message?: string
}

export interface AgentHandlers {
  onEvent: (event: AgentEvent) => void
  signal?: AbortSignal
}

/**
 * Startet eine orchestrierte Diagnoserunde und liefert die typisierten Agenten-Ereignisse
 * fortlaufend. Freier Antworttext und Aktionen werden getrennt behandelt. Ein Abbruch
 * erfolgt über das AbortSignal.
 */
export async function runAgentChat(request: OllamaChatRequest, handlers: AgentHandlers): Promise<void> {
  let response: Response
  try {
    response = await fetch('/api/ollama/chat', {
      method: 'POST',
      headers: authHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(request),
      signal: handlers.signal
    })
  } catch (error) {
    if ((error as Error).name === 'AbortError') return
    handlers.onEvent({ type: 'error', code: 'backend_unreachable', message: 'Das lokale Backend ist nicht erreichbar.' })
    return
  }

  if (!response.ok || !response.body) {
    handlers.onEvent({
      type: 'error',
      code: 'request_failed',
      message: `Die Anfrage ist fehlgeschlagen (${response.status}).`
    })
    return
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  try {
    for (;;) {
      const { value, done } = await reader.read()
      if (done) break

      buffer += decoder.decode(value, { stream: true })
      const lines = buffer.split('\n')
      buffer = lines.pop() ?? ''

      for (const line of lines) {
        dispatchEvent(line, handlers)
      }
    }
    dispatchEvent(buffer, handlers)
  } catch (error) {
    if ((error as Error).name === 'AbortError') return
    handlers.onEvent({
      type: 'error',
      code: 'stream_interrupted',
      message: 'Die Streaming-Verbindung wurde unterbrochen.'
    })
  }
}

function dispatchEvent(line: string, handlers: AgentHandlers): void {
  const trimmed = line.trim()
  if (!trimmed) return

  let event: AgentEvent
  try {
    event = JSON.parse(trimmed) as AgentEvent
  } catch {
    // Fehlerhafte JSON-Zeile ignorieren, Stream läuft weiter.
    return
  }

  handlers.onEvent(event)
}
