import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useReducer,
  type ReactNode
} from 'react'
import type {
  ChatMessage,
  Cause,
  DiagnosisNodeData,
  ExecutionState
} from '../../diagnosis/types'
import type { EventItem } from '../../types'
import { casesReducer, type CasesState } from './casesReducer'
import { createEmptyCase } from './caseFactory'
import type { CaseStatus, DiagnosisCase } from './casesTypes'
import type { AgentEvidence, AgentGraphNode, AgentGraphNodePatch } from '../ollama/ollamaApi'

interface CasesContextValue {
  cases: DiagnosisCase[]
  activeCase: DiagnosisCase
  activeId: string
  createCase: (title: string, model: string | null) => string
  selectCase: (id: string) => void
  setCaseModel: (model: string) => void
  setCaseStatus: (status: CaseStatus) => void
  addMessage: (message: ChatMessage) => void
  appendToMessage: (id: string, text: string) => void
  updateMessage: (id: string, patch: Partial<ChatMessage>) => void
  setActionState: (actionId: string, state: ExecutionState) => void
  setActionResult: (actionId: string, state: ExecutionState, result: import('../../diagnosis/types').ActionExecution, error?: string | null) => void
  setCauses: (causes: Cause[]) => void
  addEventCandidate: (event: EventItem) => void
  skipAction: (actionId: string, targetNodeId: string) => void
  addProblemNode: (id: string, title: string, description: string) => void
  initializeWorkflow: () => void
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

export function CasesProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(casesReducer, undefined, createInitialState)

  const activeCase = useMemo(
    () => state.cases.find((c) => c.id === state.activeId) ?? state.cases[0],
    [state.cases, state.activeId]
  )

  const createCase = useCallback((title: string, model: string | null) => {
    const newCase = createEmptyCase(title, model)
    dispatch({ type: 'CREATE_CASE', newCase })
    return newCase.id
  }, [])

  const addProblemNode = useCallback(
    (id: string, title: string, description: string) =>
      dispatch({ type: 'ADD_PROBLEM_NODE', id, title, description }),
    []
  )
  const initializeWorkflow = useCallback(() => dispatch({ type: 'INITIALIZE_WORKFLOW' }), [])
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
  const setActionResult = useCallback(
    (actionId: string, actionState: ExecutionState, result: import('../../diagnosis/types').ActionExecution, error?: string | null) =>
      dispatch({ type: 'SET_ACTION_RESULT', actionId, state: actionState, result, error }),
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

  const value = useMemo<CasesContextValue>(
    () => ({
      cases: state.cases,
      activeCase,
      activeId: state.activeId,
      createCase,
      selectCase,
      setCaseModel,
      setCaseStatus,
      addMessage,
      appendToMessage,
      updateMessage,
      setActionState,
      setActionResult,
      setCauses,
      addEventCandidate,
      skipAction,
      addProblemNode,
      initializeWorkflow,
      agentAddNode,
      agentPatchNode,
      agentAddEvidence
    }),
    [
      state.cases,
      state.activeId,
      activeCase,
      createCase,
      selectCase,
      setCaseModel,
      setCaseStatus,
      addMessage,
      appendToMessage,
      updateMessage,
      setActionState,
      setActionResult,
      setCauses,
      addEventCandidate,
      skipAction,
      addProblemNode,
      initializeWorkflow,
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
