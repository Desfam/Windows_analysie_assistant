import { useCallback, useEffect, useMemo, useReducer, useRef } from 'react'
import type {
  ChatMessage,
  Cause,
  DiagnosisAction,
  DiagnosisCase,
  DiagnosisEdge,
  DiagnosisNode,
  DiagnosisNodeData,
  EdgeState,
  ExecutionState
} from '../types'
import {
  buildEventsFollowUp,
  initialCase,
  initialCauses,
  initialChat,
  initialEdges,
  initialNodes
} from '../data/demoData'

interface SeedNode {
  id: string
  data: DiagnosisNodeData
}

interface SeedEdge {
  id: string
  source: string
  target: string
  label?: string
}

interface State {
  caseInfo: DiagnosisCase
  messages: ChatMessage[]
  nodes: DiagnosisNode[]
  edges: DiagnosisEdge[]
  causes: Cause[]
  selectedNodeId: string | null
}

type Action =
  | { type: 'ADD_MESSAGE'; message: ChatMessage }
  | { type: 'SET_ACTION_STATE'; actionId: string; state: ExecutionState }
  | { type: 'PATCH_NODE'; id: string; patch: Partial<DiagnosisNodeData> }
  | { type: 'ADD_NODE'; node: SeedNode }
  | { type: 'ADD_EDGE'; edge: SeedEdge; state: EdgeState }
  | { type: 'SET_EDGE_STATE'; id: string; state: EdgeState }
  | { type: 'SET_CAUSES'; causes: Cause[] }
  | { type: 'SELECT_NODE'; id: string | null }
  | { type: 'SET_CASE_STATUS'; status: string }

function toNode(seed: SeedNode): DiagnosisNode {
  return {
    id: seed.id,
    type: 'diagnosis',
    position: { x: 0, y: 0 },
    data: seed.data
  }
}

function toEdge(seed: SeedEdge, state: EdgeState): DiagnosisEdge {
  return {
    id: seed.id,
    source: seed.source,
    target: seed.target,
    type: 'diagnosis',
    data: { label: seed.label, state }
  }
}

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'ADD_MESSAGE':
      return { ...state, messages: [...state.messages, action.message] }

    case 'SET_ACTION_STATE':
      return {
        ...state,
        messages: state.messages.map((message) =>
          message.action?.id === action.actionId
            ? { ...message, action: { ...message.action, state: action.state } }
            : message
        )
      }

    case 'PATCH_NODE':
      return {
        ...state,
        nodes: state.nodes.map((node) =>
          node.id === action.id ? { ...node, data: { ...node.data, ...action.patch } } : node
        )
      }

    case 'ADD_NODE':
      if (state.nodes.some((node) => node.id === action.node.id)) {
        return state
      }
      return { ...state, nodes: [...state.nodes, toNode(action.node)] }

    case 'ADD_EDGE':
      if (state.edges.some((edge) => edge.id === action.edge.id)) {
        return state
      }
      return { ...state, edges: [...state.edges, toEdge(action.edge, action.state)] }

    case 'SET_EDGE_STATE':
      return {
        ...state,
        edges: state.edges.map((edge) =>
          edge.id === action.id ? { ...edge, data: { ...edge.data, state: action.state } } : edge
        )
      }

    case 'SET_CAUSES':
      return { ...state, causes: action.causes }

    case 'SELECT_NODE':
      return { ...state, selectedNodeId: action.id }

    case 'SET_CASE_STATUS':
      return { ...state, caseInfo: { ...state.caseInfo, status: action.status } }

    default:
      return state
  }
}

function createInitialState(): State {
  return {
    caseInfo: initialCase,
    messages: initialChat,
    nodes: initialNodes.map(toNode),
    edges: initialEdges.map((edge) => toEdge(edge, 'pending')),
    causes: initialCauses,
    selectedNodeId: null
  }
}

function nowTime(): string {
  return new Date().toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
}

let messageCounter = 100

export function useDiagnosis() {
  const [state, dispatch] = useReducer(reducer, undefined, createInitialState)
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

  const sendMessage = useCallback((text: string) => {
    const trimmed = text.trim()
    if (!trimmed) return
    dispatch({
      type: 'ADD_MESSAGE',
      message: {
        id: `msg-${(messageCounter += 1)}`,
        role: 'user',
        text: trimmed,
        timestamp: nowTime()
      }
    })
  }, [])

  const selectNode = useCallback((id: string | null) => {
    dispatch({ type: 'SELECT_NODE', id })
  }, [])

  const skipAction = useCallback((action: DiagnosisAction) => {
    dispatch({ type: 'SET_ACTION_STATE', actionId: action.id, state: 'skipped' })
    dispatch({ type: 'PATCH_NODE', id: action.targetNodeId, patch: { state: 'skipped' } })
  }, [])

  const runAction = useCallback(
    (action: DiagnosisAction) => {
      if (runningRef.current || action.state === 'completed') {
        return
      }
      if (action.targetNodeId !== 'events') {
        return
      }
      runningRef.current = true

      const follow = buildEventsFollowUp()

      // 1. Aktion und Knoten in Ausführung versetzen, eingehende Verbindung aktivieren.
      dispatch({ type: 'SET_ACTION_STATE', actionId: action.id, state: 'running' })
      dispatch({ type: 'PATCH_NODE', id: 'events', patch: { state: 'running', startedAt: nowTime() } })
      dispatch({ type: 'SET_EDGE_STATE', id: 'e-problem-events', state: 'active' })

      // 2. Nach kurzer simulierter Verarbeitung abschließen.
      schedule(() => {
        dispatch({ type: 'SET_ACTION_STATE', actionId: action.id, state: 'completed' })
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

      // 3. Neue Knoten nacheinander einblenden, Verbindungen passend einfärben.
      const [evNode, decisionNode, nvmeNode, evaluateNode, memoryNode] = follow.nodes

      schedule(() => {
        dispatch({ type: 'ADD_NODE', node: evNode })
        dispatch({ type: 'ADD_EDGE', edge: follow.edges[0], state: 'completed' })
      }, 2200)

      schedule(() => {
        dispatch({ type: 'ADD_NODE', node: decisionNode })
        dispatch({ type: 'ADD_EDGE', edge: follow.edges[1], state: 'completed' })
      }, 2450)

      schedule(() => {
        dispatch({ type: 'ADD_NODE', node: nvmeNode })
        dispatch({ type: 'ADD_EDGE', edge: follow.edges[2], state: 'active' })
      }, 2700)

      schedule(() => {
        dispatch({ type: 'ADD_NODE', node: evaluateNode })
        dispatch({ type: 'ADD_EDGE', edge: follow.edges[3], state: 'pending' })
      }, 2950)

      schedule(() => {
        dispatch({ type: 'ADD_NODE', node: memoryNode })
        dispatch({ type: 'ADD_EDGE', edge: follow.edges[4], state: 'discarded' })
      }, 3200)

      // 4. Nächsten Knoten bereitstellen, Ursachen und Chat aktualisieren.
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
            id: `msg-${(messageCounter += 1)}`,
            role: 'assistant',
            text: follow.summary,
            timestamp: nowTime()
          }
        })
        runningRef.current = false
      }, 3450)
    },
    [schedule]
  )

  const selectedNode = useMemo(
    () => state.nodes.find((node) => node.id === state.selectedNodeId) ?? null,
    [state.nodes, state.selectedNodeId]
  )

  return {
    ...state,
    selectedNode,
    sendMessage,
    runAction,
    skipAction,
    selectNode
  }
}
