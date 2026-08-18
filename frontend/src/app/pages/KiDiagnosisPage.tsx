import { useEffect, useMemo, useRef, useState } from 'react'
import { MessageSquare, Network } from 'lucide-react'
import { DiagnosisChat } from '../../diagnosis/components/DiagnosisChat'
import { CaseOverview } from '../../diagnosis/components/CaseOverview'
import { CommandDetailsDialog } from '../../diagnosis/components/CommandDetailsDialog'
import { usePrefersReducedMotion } from '../../diagnosis/lib/styles'
import type { AgentStatus, DiagnosisAction, DiagnosisCase as CaseInfo } from '../../diagnosis/types'
import { useCases, nextMessageId } from '../cases/CasesContext'
import { useOllama } from '../ollama/OllamaContext'
import { runAgentChat, type AgentEvent, type OllamaChatMessage } from '../ollama/ollamaApi'

const actionDetails: Record<string, Omit<DiagnosisAction, 'id' | 'state' | 'targetNodeId'>> = {
  'events.query': {
    title: 'Windows-Ereignisse untersuchen',
    description: 'Ereignisprotokolle nach Fehlern und Warnungen durchsuchen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 10 Sekunden', note: 'Es werden nur Protokolle gelesen. Am System wird nichts verändert.',
    command: 'Serverseitige, validierte Ereignisabfrage'
  },
  'winget.status': {
    title: 'Winget-Status prüfen', description: 'Verfügbarkeit, Version und Aufrufbarkeit von winget prüfen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 5 Sekunden', note: 'Es wird ausschließlich der Status von winget gelesen.',
    command: 'winget.exe --version'
  },
  'winget.sources.list': {
    title: 'Winget-Quellen prüfen', description: 'Konfigurierte Quellen und erkennbare Quellenfehler lesen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 10 Sekunden', note: 'Quellen werden nur angezeigt, nicht zurückgesetzt oder verändert.',
    command: 'winget.exe source list'
  },
  'appinstaller.status': {
    title: 'App Installer prüfen', description: 'Installations- und Paketstatus von Microsoft.DesktopAppInstaller lesen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 5 Sekunden', note: 'Die Paketregistrierung wird nur abgefragt.',
    command: 'Get-AppxPackage Microsoft.DesktopAppInstaller'
  },
  'windowsupdate.status': {
    title: 'Windows Update prüfen', description: 'Updatezustand und einen ausstehenden Neustart prüfen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 5 Sekunden', note: 'Dienste werden nicht gestartet, beendet oder verändert.',
    command: 'Serverseitige Statusprüfung'
  },
  'storage.summary': {
    title: 'Datenträgerstatus prüfen', description: 'Lokale Datenträger und den freien Speicher zusammenfassen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 5 Sekunden', note: 'Es werden nur Datenträgerinformationen gelesen.',
    command: 'Serverseitige Datenträgerabfrage'
  },
  'network.microsoftEndpoints': {
    title: 'Microsoft-Endpunkte prüfen', description: 'DNS-Auflösung der erforderlichen Microsoft-Endpunkte prüfen.',
    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }, risk: 'R0',
    estimatedDuration: 'ca. 5 Sekunden', note: 'Kein Portscan und keine Netzwerkkonfiguration werden ausgeführt.',
    command: 'DNS-Auflösung fester Microsoft-Endpunkte'
  }
}

const statusLabels: Record<string, string> = {
  open: 'Offen',
  running: 'Untersuchung läuft',
  waiting: 'Wartet auf Eingabe',
  resolved: 'Gelöst',
  closed: 'Geschlossen'
}

// Fehlercodes, bei denen die Orchestrierung intern weiterläuft (kein Streaming-Abbruch).
const NON_FATAL_ERROR_CODES = new Set(['invalid_tool_call'])

export function KiDiagnosisPage() {
  const {
    activeCase,
    addMessage,
    appendToMessage,
    updateMessage,
    skipAction,
    addProblemNode,
    agentAddNode,
    agentPatchNode,
    agentAddEvidence,
    setActionState,
    setActionResult
  } = useCases()
  const { selectedModel, phase } = useOllama()
  const animate = !usePrefersReducedMotion()

  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null)
  const [commandAction, setCommandAction] = useState<DiagnosisAction | null>(null)
  const [isStreaming, setIsStreaming] = useState(false)
  const [agentStatus, setAgentStatus] = useState<AgentStatus | null>(null)
  const controllerRef = useRef<AbortController | null>(null)
  const assistantIdRef = useRef<string | null>(null)

  const selectedNode = useMemo(
    () => activeCase.nodes.find((n) => n.id === selectedNodeId) ?? null,
    [activeCase.nodes, selectedNodeId]
  )

  const caseInfo: CaseInfo = {
    name: activeCase.title,
    status: statusLabels[activeCase.status] ?? activeCase.status
  }

  const canSend = selectedModel != null && phase !== 'unreachable'
  const disabledReason =
    selectedModel == null
      ? 'Bitte zuerst ein Ollama-Modell in der Seitenleiste auswählen.'
      : phase === 'unreachable'
        ? 'Ollama ist nicht erreichbar. Bitte Verbindung in den Einstellungen prüfen.'
        : undefined

  const now = () => new Date().toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })

  const finishStreaming = (phase = 'completed', title = 'Diagnose abgeschlossen') => {
    setIsStreaming(false)
    setAgentStatus({ phase, title, description: '', startedAt: Date.now() })
    controllerRef.current = null
  }

  const handleAgentEvent = (assistantId: string, event: AgentEvent) => {
    switch (event.type) {
      case 'assistant.delta':
        if (event.content) appendToMessage(assistantId, event.content)
        break
      case 'assistant.completed':
        updateMessage(assistantId, { streaming: false, durationMs: event.durationMs })
        finishStreaming()
        break
      case 'agent.status':
        if (event.phase && event.title && event.description) {
          setAgentStatus({ phase: event.phase, title: event.title, description: event.description, startedAt: Date.now() })
        }
        break
      case 'graph.nodeAdded':
        if (event.node) agentAddNode(event.node)
        break
      case 'action.proposed': {
        const definition = event.actionId ? actionDetails[event.actionId] : undefined
        if (definition && event.executionId) {
          addMessage({
            id: nextMessageId(), role: 'assistant', text: '', timestamp: now(),
            action: { ...definition, id: event.executionId, state: 'ready', targetNodeId: event.nodeId ?? '' }
          })
        }
        break
      }
      case 'action.started':
        if (event.executionId) setActionState(event.executionId, 'running')
        break
      case 'action.completed': {
        const terminalState = event.actionState === 'cancelled' ? 'cancelled' : event.result?.success ? 'completed' : 'failed'
        if (event.executionId && event.result?.execution) {
          setActionResult(event.executionId, terminalState, event.result.execution, event.result.error)
        } else if (event.executionId) {
          setActionState(event.executionId, terminalState)
        }
        break
      }
      case 'graph.nodeUpdated':
        if (event.nodePatch) agentPatchNode(event.nodePatch)
        break
      case 'evidence.added':
        if (event.evidence) agentAddEvidence(event.evidence)
        break
      case 'error':
        if (event.code && NON_FATAL_ERROR_CODES.has(event.code)) {
          // Ungültiger Werkzeugaufruf: Die Orchestrierung läuft intern weiter.
          break
        }
        updateMessage(assistantId, {
          streaming: false,
          error: event.message ?? 'Die Anfrage ist fehlgeschlagen.'
        })
        finishStreaming(event.code === 'timeout' ? 'timeout' : 'failed', event.message ?? 'Diagnose fehlgeschlagen')
        break
      default:
        break
    }
  }

  const handleSend = (text: string) => {
    if (!selectedModel || isStreaming) return

    if (activeCase.nodes.length === 0) {
      const description = text.length > 300 ? `${text.slice(0, 300)} …` : text
      addProblemNode('problem', 'Problem gemeldet', description)
    }

    const history: OllamaChatMessage[] = activeCase.messages
      .filter((m) => m.text.trim().length > 0 && !m.error)
      .map((m) => ({ role: m.role, content: m.text }))
    history.push({ role: 'user', content: text })

    addMessage({ id: nextMessageId(), role: 'user', text, timestamp: now() })

    const assistantId = nextMessageId()
    assistantIdRef.current = assistantId
    addMessage({
      id: assistantId,
      role: 'assistant',
      text: '',
      timestamp: now(),
      streaming: true,
      model: selectedModel
    })

    const controller = new AbortController()
    controllerRef.current = controller
    setIsStreaming(true)
    setAgentStatus({ phase: 'understanding', title: 'Problem wird analysiert', description: 'Die Anfrage wird für die Diagnose eingeordnet.', startedAt: Date.now() })

    const caseContext = {
      currentEvidence: activeCase.evidence.map(
        (e) => `Ereignis ${e.eventId ?? '—'} (${e.source ?? 'unbekannt'}): ${e.summary}`
      )
    }

    void runAgentChat(
      { model: selectedModel, messages: history, caseContext },
      { signal: controller.signal, onEvent: (event) => handleAgentEvent(assistantId, event) }
    )
  }

  const handleCancel = () => {
    controllerRef.current?.abort()
    controllerRef.current = null
    if (assistantIdRef.current) {
      updateMessage(assistantIdRef.current, { streaming: false, aborted: true })
    }
    setIsStreaming(false)
    setAgentStatus({ phase: 'cancelled', title: 'Diagnose abgebrochen', description: '', startedAt: Date.now() })
  }

  const chat = (
    <DiagnosisChat
      messages={activeCase.messages}
      animate={animate}
      canSend={canSend}
      isStreaming={isStreaming}
      status={agentStatus}
      disabledReason={disabledReason}
      onSend={handleSend}
      onCancel={handleCancel}
      onShowCommand={setCommandAction}
      onSkip={(action) => skipAction(action.id, action.targetNodeId)}
    />
  )

  const overview = (
    <CaseOverview
      caseInfo={caseInfo}
      nodes={activeCase.nodes}
      edges={activeCase.edges}
      causes={activeCase.causes}
      selectedNode={selectedNode}
      onSelectNode={setSelectedNodeId}
    />
  )

  return (
    <div className="flex h-full flex-col">
      <ResponsiveSplit chat={chat} overview={overview} />
      <CommandDetailsDialog action={commandAction} onClose={() => setCommandAction(null)} />
    </div>
  )
}

function ResponsiveSplit({ chat, overview }: { chat: React.ReactNode; overview: React.ReactNode }) {
  const [narrow, setNarrow] = useState(
    () => typeof window !== 'undefined' && window.matchMedia('(max-width: 1000px)').matches
  )
  const [tab, setTab] = useState<'chat' | 'flow'>('chat')

  useEffect(() => {
    const query = window.matchMedia('(max-width: 1000px)')
    const handler = (e: MediaQueryListEvent) => setNarrow(e.matches)
    query.addEventListener('change', handler)
    return () => query.removeEventListener('change', handler)
  }, [])

  if (!narrow) {
    return (
      <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,55fr)_minmax(0,45fr)]">
        <section className="min-h-0 overflow-hidden border-r border-white/[0.06]">{chat}</section>
        <section className="min-h-0 overflow-hidden">{overview}</section>
      </div>
    )
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex gap-1 border-b border-white/[0.06] px-3 py-2">
        <TabButton active={tab === 'chat'} onClick={() => setTab('chat')} icon={<MessageSquare className="h-4 w-4" />}>
          Chat
        </TabButton>
        <TabButton active={tab === 'flow'} onClick={() => setTab('flow')} icon={<Network className="h-4 w-4" />}>
          Diagnoseverlauf
        </TabButton>
      </div>
      <div className="min-h-0 flex-1">{tab === 'chat' ? chat : overview}</div>
    </div>
  )
}

function TabButton({
  active,
  onClick,
  icon,
  children
}: {
  active: boolean
  onClick: () => void
  icon: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
        active ? 'bg-blue-500/15 text-blue-200' : 'text-slate-400 hover:bg-white/[0.04] hover:text-slate-200'
      }`}
    >
      {icon}
      {children}
    </button>
  )
}
