import type {
  ChatMessage,
  Cause,
  DiagnosisNodeData,
  EdgeState,
  Evidence,
  ExecutionState,
  NodeKind,
  RiskLevel
} from '../../diagnosis/types'
import type { CaseStatus, DiagnosisCase } from './casesTypes'
import { toEdge, toNode, type SeedEdge, type SeedNode } from './caseFactory'
import type { AgentEvidence, AgentGraphNode, AgentGraphNodePatch } from '../ollama/ollamaApi'

export interface CasesState {
  cases: DiagnosisCase[]
  activeId: string
}

export type CasesAction =
  | { type: 'ADD_MESSAGE'; message: ChatMessage }
  | { type: 'APPEND_TO_MESSAGE'; id: string; text: string }
  | { type: 'UPDATE_MESSAGE'; id: string; patch: Partial<ChatMessage> }
  | { type: 'SET_ACTION_STATE'; actionId: string; state: ExecutionState }
  | { type: 'PATCH_NODE'; id: string; patch: Partial<DiagnosisNodeData> }
  | { type: 'ADD_NODE'; node: SeedNode }
  | { type: 'ADD_EDGE'; edge: SeedEdge; state: EdgeState }
  | { type: 'SET_EDGE_STATE'; id: string; state: EdgeState }
  | { type: 'SET_CAUSES'; causes: Cause[] }
  | { type: 'ADD_EVENT_CANDIDATE'; node: SeedNode; evidence: Evidence; eventId: string }
  | { type: 'SET_CASE_STATUS'; status: CaseStatus }
  | { type: 'SET_CASE_MODEL'; model: string }
  | { type: 'CREATE_CASE'; newCase: DiagnosisCase }
  | { type: 'SELECT_CASE'; id: string }
  | { type: 'ADD_PROBLEM_NODE'; id: string; title: string; description: string }
  | { type: 'AGENT_ADD_NODE'; node: AgentGraphNode }
  | { type: 'AGENT_PATCH_NODE'; patch: AgentGraphNodePatch }
  | { type: 'AGENT_ADD_EVIDENCE'; evidence: AgentEvidence }

function agentStateToExecution(state: string): ExecutionState {
  const allowed: ExecutionState[] = [
    'pending', 'ready', 'running', 'completed', 'failed',
    'skipped', 'cancelled', 'waitingForApproval', 'waitingForUser', 'blocked'
  ]
  return allowed.includes(state as ExecutionState) ? (state as ExecutionState) : 'pending'
}

function edgeStateFor(state: ExecutionState): EdgeState {
  switch (state) {
    case 'running':
      return 'active'
    case 'completed':
      return 'completed'
    case 'failed':
    case 'cancelled':
    case 'skipped':
      return 'discarded'
    default:
      return 'pending'
  }
}

function toRisk(level: string): RiskLevel {
  return level === 'R1' || level === 'R2' || level === 'R3' ? level : 'R0'
}

function toKind(kind: string): NodeKind {
  const allowed: NodeKind[] = [
    'problem', 'action', 'decision', 'evidence', 'repair', 'verification', 'completion', 'userQuery'
  ]
  return allowed.includes(kind as NodeKind) ? (kind as NodeKind) : 'action'
}

/** Bestimmt den Elternknoten für eine neue Kante (bevorzugt zuletzt hinzugefügte Aktion/Problem). */
function findParentId(nodes: DiagnosisCase['nodes'], preferKind?: NodeKind): string | null {
  if (nodes.length === 0) return null
  if (preferKind) {
    for (let i = nodes.length - 1; i >= 0; i--) {
      if (nodes[i].data.kind === preferKind) return nodes[i].id
    }
  }
  const problem = nodes.find((n) => n.data.kind === 'problem')
  if (problem) return problem.id
  return nodes[nodes.length - 1].id
}

function updateActive(state: CasesState, fn: (c: DiagnosisCase) => DiagnosisCase): CasesState {
  const now = new Date().toISOString()
  return {
    ...state,
    cases: state.cases.map((item) =>
      item.id === state.activeId ? { ...fn(item), updatedAt: now } : item
    )
  }
}

export function casesReducer(state: CasesState, action: CasesAction): CasesState {
  switch (action.type) {
    case 'CREATE_CASE':
      return { cases: [...state.cases, action.newCase], activeId: action.newCase.id }

    case 'SELECT_CASE':
      return state.cases.some((c) => c.id === action.id)
        ? { ...state, activeId: action.id }
        : state

    case 'ADD_MESSAGE':
      return updateActive(state, (c) => ({ ...c, messages: [...c.messages, action.message] }))

    case 'APPEND_TO_MESSAGE':
      return updateActive(state, (c) => ({
        ...c,
        messages: c.messages.map((m) =>
          m.id === action.id ? { ...m, text: m.text + action.text } : m
        )
      }))

    case 'UPDATE_MESSAGE':
      return updateActive(state, (c) => ({
        ...c,
        messages: c.messages.map((m) => (m.id === action.id ? { ...m, ...action.patch } : m))
      }))

    case 'SET_ACTION_STATE':
      return updateActive(state, (c) => ({
        ...c,
        messages: c.messages.map((m) =>
          m.action?.id === action.actionId
            ? { ...m, action: { ...m.action, state: action.state } }
            : m
        )
      }))

    case 'PATCH_NODE':
      return updateActive(state, (c) => ({
        ...c,
        nodes: c.nodes.map((n) =>
          n.id === action.id ? { ...n, data: { ...n.data, ...action.patch } } : n
        )
      }))

    case 'ADD_NODE':
      return updateActive(state, (c) =>
        c.nodes.some((n) => n.id === action.node.id)
          ? c
          : { ...c, nodes: [...c.nodes, toNode(action.node)] }
      )

    case 'ADD_EDGE':
      return updateActive(state, (c) =>
        c.edges.some((e) => e.id === action.edge.id)
          ? c
          : { ...c, edges: [...c.edges, toEdge(action.edge, action.state)] }
      )

    case 'SET_EDGE_STATE':
      return updateActive(state, (c) => ({
        ...c,
        edges: c.edges.map((e) =>
          e.id === action.id ? { ...e, data: { ...e.data!, state: action.state } } : e
        )
      }))

    case 'SET_CAUSES':
      return updateActive(state, (c) => ({ ...c, causes: action.causes }))

    case 'ADD_EVENT_CANDIDATE':
      return updateActive(state, (c) =>
        c.selectedEventIds.includes(action.eventId)
          ? c
          : {
              ...c,
              nodes: c.nodes.some((n) => n.id === action.node.id)
                ? c.nodes
                : [...c.nodes, toNode(action.node)],
              evidence: [...c.evidence, action.evidence],
              selectedEventIds: [...c.selectedEventIds, action.eventId]
            }
      )

    case 'SET_CASE_STATUS':
      return updateActive(state, (c) => ({ ...c, status: action.status }))

    case 'SET_CASE_MODEL':
      return updateActive(state, (c) => ({ ...c, modelName: action.model }))

    case 'ADD_PROBLEM_NODE':
      return updateActive(state, (c) =>
        c.nodes.some((n) => n.data.kind === 'problem')
          ? c
          : {
              ...c,
              nodes: [
                ...c.nodes,
                toNode({
                  id: action.id,
                  data: {
                    kind: 'problem',
                    title: action.title,
                    description: action.description,
                    state: 'completed',
                    risk: 'R0',
                    systemImpact: { changesSystem: false, label: 'Keine Systemänderung' }
                  }
                })
              ]
            }
      )

    case 'AGENT_ADD_NODE': {
      const execState = agentStateToExecution(action.node.state)
      return updateActive(state, (c) => {
        if (c.nodes.some((n) => n.id === action.node.id)) {
          return c
        }
        const parentId = findParentId(c.nodes)
        const seed: SeedNode = {
          id: action.node.id,
          data: {
            kind: toKind(action.node.kind),
            title: action.node.title,
            description: action.node.description,
            state: execState,
            risk: toRisk(action.node.riskLevel),
            systemImpact: {
              changesSystem: action.node.changesSystem,
              label: action.node.changesSystem ? 'Systemänderung' : 'Keine Systemänderung'
            }
          }
        }
        const nodes = [...c.nodes, toNode(seed)]
        const edges =
          parentId != null
            ? [...c.edges, toEdge({ id: `e-${parentId}-${action.node.id}`, source: parentId, target: action.node.id }, edgeStateFor(execState))]
            : c.edges
        return { ...c, nodes, edges }
      })
    }

    case 'AGENT_PATCH_NODE': {
      const execState = agentStateToExecution(action.patch.state)
      return updateActive(state, (c) => ({
        ...c,
        nodes: c.nodes.map((n) =>
          n.id === action.patch.id
            ? {
                ...n,
                data: {
                  ...n.data,
                  state: execState,
                  result: action.patch.result ?? n.data.result,
                  reason: action.patch.error ?? n.data.reason
                }
              }
            : n
        ),
        edges: c.edges.map((e) =>
          e.target === action.patch.id ? { ...e, data: { ...e.data!, state: edgeStateFor(execState) } } : e
        )
      }))
    }

    case 'AGENT_ADD_EVIDENCE':
      return updateActive(state, (c) => {
        const evidenceId = action.evidence.id
        if (c.nodes.some((n) => n.id === evidenceId)) {
          return c
        }
        const parentId = findParentId(c.nodes, 'action')
        const title = action.evidence.eventId != null
          ? `Ereignis ${action.evidence.eventId} · ${action.evidence.provider ?? 'unbekannt'}`
          : 'Beleg'
        const seed: SeedNode = {
          id: evidenceId,
          data: {
            kind: 'evidence',
            title,
            description: action.evidence.summary,
            state: 'completed',
            risk: 'R0',
            systemImpact: { changesSystem: false, label: 'Keine Systemänderung' },
            result: action.evidence.summary
          }
        }
        const nodes = [...c.nodes, toNode(seed)]
        const edges =
          parentId != null
            ? [...c.edges, toEdge({ id: `e-${parentId}-${evidenceId}`, source: parentId, target: evidenceId }, 'completed')]
            : c.edges
        const evidence: Evidence = {
          id: evidenceId,
          eventId: action.evidence.eventId ?? undefined,
          source: action.evidence.provider ?? undefined,
          summary: action.evidence.summary
        }
        return { ...c, nodes, edges, evidence: [...c.evidence, evidence] }
      })

    default:
      return state
  }
}
