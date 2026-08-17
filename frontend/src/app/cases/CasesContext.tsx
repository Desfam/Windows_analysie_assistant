import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
  type ReactNode
} from 'react'
import type {
  ChatMessage,
  Cause,
  DiagnosisNodeData,
  EdgeState,
  ExecutionState
} from '../../diagnosis/types'
import type { EventItem } from '../../types'
import { buildEventsFollowUp } from '../../diagnosis/data/demoData'
import { casesReducer, type CasesState } from './casesReducer'
import { createDemoCase, createEmptyCase } from './caseFactory'
import type { CaseStatus, DiagnosisCase } from './casesTypes'
import type { AgentEvidence, AgentGraphNode, AgentGraphNodePatch } from '../ollama/ollamaApi'

interface CasesContextValue {
  cases: DiagnosisCase[]
  activeCase: DiagnosisCase
  activeId: string
  isRunningDemo: boolean
  createCase: (title: string, model: string | null) => string
  loadDemoCase: (model: string | null) => string
  selectCase: (id: string) => void
  setCaseModel: (model: string) => void
  setCaseStatus: (status: CaseStatus) => void
  addMessage: (message: ChatMessage) => void
  appendToMessage: (id: string, text: string) => void
  updateMessage: (id: string, patch: Partial<ChatMessage>) => void
  setActionState: (actionId: string, state: ExecutionState) => void
  setCauses: (causes: Cause[]) => void
  addEventCandidate: (event: EventItem) => void
  skipAction: (actionId: string, targetNodeId: string) => void
  runEventsDemo: () => void
  addProblemNode: (id: string, title: string, description: string) => void
  agentAddNode: (node: AgentGraphNode) => void
  agentPatchNode: (patch: AgentGraphNodePatch) => void
  agentAddEvidence: (evidence: AgentEvidence) => void
}

const CasesContext = createContext<CasesContextValue | null>(null)

function createInitialState(): CasesState {
  const initial = createEmptyCase('Diagnosefall 1', null)
  return { cases: [initial], activeId: initial.id }
}

let messageCounter = 0
export function nextMessageId(): string {
  messageCounter += 1
  return `msg-${Date.now()}-${messageCounter}`
}

function nowTime(): string {
  return new Date().toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

export function CasesProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(casesReducer, undefined, createInitialState)
  const [isRunningDemo, setIsRunningDemo] = useState(false)
  const timers = useRef<number[]>([])
  const runningRef = useRef(false)

  useEffect(() => {
    const pending = timers.current
    return () => pending.forEach((id) => window.clearTimeout(id))
  }, [])

  const schedule = useCallback((fn: () => void, delay: number) => {
    const id = window.setTimeout(fn, delay)
    timers.current.push(id)
  }, [])

  const activeCase = useMemo(
    () => state.cases.find((c) => c.id === state.activeId) ?? state.cases[0],
    [state.cases, state.activeId]
  )

  const createCase = useCallback((title: string, model: string | null) => {
    const newCase = createEmptyCase(title, model)
    dispatch({ type: 'CREATE_CASE', newCase })
    return newCase.id
  }, [])

  const loadDemoCase = useCallback((model: string | null) => {
    const demo = createDemoCase(model)
    dispatch({ type: 'CREATE_CASE', newCase: demo })
    return demo.id
  }, [])

  const addProblemNode = useCallback(
    (id: string, title: string, description: string) =>
      dispatch({ type: 'ADD_PROBLEM_NODE', id, title, description }),
    []
  )
  const agentAddNode = useCallback((node: AgentGraphNode) => dispatch({ type: 'AGENT_ADD_NODE', node }), [])
  const agentPatchNode = useCallback((patch: AgentGraphNodePatch) => dispatch({ type: 'AGENT_PATCH_NODE', patch }), [])
  const agentAddEvidence = useCallback(
    (evidence: AgentEvidence) => dispatch({ type: 'AGENT_ADD_EVIDENCE', evidence }),
    []
  )

  const selectCase = useCallback((id: string) => dispatch({ type: 'SELECT_CASE', id }), [])
  const setCaseModel = useCallback((model: string) => dispatch({ type: 'SET_CASE_MODEL', model }), [])
  const setCaseStatus = useCallback((status: CaseStatus) => dispatch({ type: 'SET_CASE_STATUS', status }), [])
  const addMessage = useCallback((message: ChatMessage) => dispatch({ type: 'ADD_MESSAGE', message }), [])
  const appendToMessage = useCallback(
    (id: string, text: string) => dispatch({ type: 'APPEND_TO_MESSAGE', id, text }),
    []
  )
  const updateMessage = useCallback(
    (id: string, patch: Partial<ChatMessage>) => dispatch({ type: 'UPDATE_MESSAGE', id, patch }),
    []
  )
  const setActionState = useCallback(
    (actionId: string, s: ExecutionState) => dispatch({ type: 'SET_ACTION_STATE', actionId, state: s }),
    []
  )
  const setCauses = useCallback((causes: Cause[]) => dispatch({ type: 'SET_CAUSES', causes }), [])

  const skipAction = useCallback((actionId: string, targetNodeId: string) => {
    dispatch({ type: 'SET_ACTION_STATE', actionId, state: 'skipped' })
    dispatch({ type: 'PATCH_NODE', id: targetNodeId, patch: { state: 'skipped' } })
  }, [])

  const addEventCandidate = useCallback((event: EventItem) => {
    const nodeData: DiagnosisNodeData = {
      kind: 'evidence',
      title: `Ereignis ${event.eventId} · ${event.providerName ?? 'unbekannt'}`,
      description: event.title ?? event.summary ?? 'Übergebenes Ereignis',
      state: 'pending',
      risk: 'R0',
      systemImpact: { changesSystem: false, label: 'Keine Systemänderung' },
      reason:
        'Vom Benutzer aus der Systemübersicht zur Untersuchung übergeben. ' +
        'Noch nicht als Beleg bestätigt.',
      result: event.summary ?? undefined
    }

    dispatch({
      type: 'ADD_EVENT_CANDIDATE',
      node: { id: `evt-${event.id}`, data: nodeData },
      evidence: {
        id: `evt-${event.id}`,
        eventId: event.eventId,
        source: event.providerName ?? undefined,
        summary: event.title ?? event.summary ?? 'Übergebenes Ereignis'
      },
      eventId: event.id
    })
  }, [])

  const runEventsDemo = useCallback(() => {
    if (runningRef.current) return
    const events = activeCase.nodes.find((n) => n.id === 'events')
    if (!events || events.data.state === 'completed') return

    runningRef.current = true
    setIsRunningDemo(true)
    const follow = buildEventsFollowUp()

    dispatch({ type: 'SET_ACTION_STATE', actionId: 'action-events', state: 'running' })
    dispatch({ type: 'PATCH_NODE', id: 'events', patch: { state: 'running', startedAt: nowTime() } })
    dispatch({ type: 'SET_EDGE_STATE', id: 'e-problem-events', state: 'active' })

    schedule(() => {
      dispatch({ type: 'SET_ACTION_STATE', actionId: 'action-events', state: 'completed' })
      dispatch({
        type: 'PATCH_NODE',
        id: 'events',
        patch: {
          state: 'completed',
          finishedAt: nowTime(),
          result: 'Relevante Ereignisse gefunden (Ereignis 129 · stornvme).'
        }
      })
      dispatch({ type: 'SET_EDGE_STATE', id: 'e-problem-events', state: 'completed' })
    }, 2000)

    const addAt = (delay: number, nodeIndex: number, edgeIndex: number, edgeState: EdgeState) =>
      schedule(() => {
        dispatch({ type: 'ADD_NODE', node: follow.nodes[nodeIndex] })
        dispatch({ type: 'ADD_EDGE', edge: follow.edges[edgeIndex], state: edgeState })
      }, delay)

    addAt(2200, 0, 0, 'completed')
    addAt(2450, 1, 1, 'completed')
    addAt(2700, 2, 2, 'active')
    addAt(2950, 3, 3, 'pending')
    addAt(3200, 4, 4, 'discarded')

    schedule(() => {
      dispatch({ type: 'PATCH_NODE', id: follow.readyNodeId, patch: { state: 'ready' } })
      dispatch({
        type: 'SET_CAUSES',
        causes: [
          { id: 'cause-nvme', title: 'NVMe-Treiber oder Firmware', level: 'strong' },
          { id: 'cause-ram', title: 'Arbeitsspeicher', level: 'unclear' },
          { id: 'cause-update', title: 'Windows Update', level: 'some' }
        ]
      })
      dispatch({
        type: 'ADD_MESSAGE',
        message: {
          id: nextMessageId(),
          role: 'assistant',
          text: follow.summary,
          timestamp: nowTime()
        }
      })
      runningRef.current = false
      setIsRunningDemo(false)
    }, 3450)
  }, [activeCase.nodes, schedule])

  const value = useMemo<CasesContextValue>(
    () => ({
      cases: state.cases,
      activeCase,
      activeId: state.activeId,
      isRunningDemo,
      createCase,
      loadDemoCase,
      selectCase,
      setCaseModel,
      setCaseStatus,
      addMessage,
      appendToMessage,
      updateMessage,
      setActionState,
      setCauses,
      addEventCandidate,
      skipAction,
      runEventsDemo,
      addProblemNode,
      agentAddNode,
      agentPatchNode,
      agentAddEvidence
    }),
    [
      state.cases,
      state.activeId,
      activeCase,
      isRunningDemo,
      createCase,
      loadDemoCase,
      selectCase,
      setCaseModel,
      setCaseStatus,
      addMessage,
      appendToMessage,
      updateMessage,
      setActionState,
      setCauses,
      addEventCandidate,
      skipAction,
      runEventsDemo,
      addProblemNode,
      agentAddNode,
      agentPatchNode,
      agentAddEvidence
    ]
  )

  return <CasesContext.Provider value={value}>{children}</CasesContext.Provider>
}

export function useCases(): CasesContextValue {
  const context = useContext(CasesContext)
  if (!context) {
    throw new Error('useCases muss innerhalb von CasesProvider verwendet werden.')
  }
  return context
}
