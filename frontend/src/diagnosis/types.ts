import type { Node, Edge } from '@xyflow/react'

export type ExecutionState =
  | 'pending'
  | 'ready'
  | 'running'
  | 'completed'
  | 'failed'
  | 'skipped'
  | 'cancelled'
  | 'waitingForApproval'
  | 'waitingForUser'
  | 'blocked'

export type RiskLevel = 'R0' | 'R1' | 'R2' | 'R3'

export type NodeKind =
  | 'problem'
  | 'action'
  | 'decision'
  | 'evidence'
  | 'repair'
  | 'verification'
  | 'completion'
  | 'userQuery'

export type EdgeState = 'pending' | 'active' | 'completed' | 'discarded'

export type EvidenceLevel = 'strong' | 'some' | 'unclear' | 'ruledOut'

export interface SystemImpact {
  changesSystem: boolean
  label: string
}

export interface Evidence {
  id: string
  eventId?: number
  source?: string
  summary: string
}

export interface Cause {
  id: string
  title: string
  level: EvidenceLevel
}

/**
 * Datenmodell eines Diagnoseknotens. Als `type` definiert, damit es die
 * Record-Randbedingung von React Flow (`@xyflow/react`) erfüllt.
 */
export type DiagnosisNodeData = {
  kind: NodeKind
  title: string
  description: string
  state: ExecutionState
  risk: RiskLevel
  systemImpact: SystemImpact
  estimatedDuration?: string
  demoCommand?: string
  condition?: string
  result?: string
  requiresApproval?: boolean
  evidence?: Evidence[]
  reason?: string
  startedAt?: string
  finishedAt?: string
  nextSteps?: string[]
}

export type DiagnosisEdgeData = {
  label?: string
  state: EdgeState
}

export type DiagnosisNode = Node<DiagnosisNodeData, 'diagnosis'>
export type DiagnosisEdge = Edge<DiagnosisEdgeData>

export interface DiagnosisAction {
  id: string
  title: string
  description: string
  systemImpact: SystemImpact
  risk: RiskLevel
  estimatedDuration: string
  note: string
  command: string
  state: ExecutionState
  targetNodeId: string
  execution?: ActionExecution
  error?: string
}

export interface ActionExecution {
  program: string
  arguments: string[]
  startedAt: string
  completedAt: string
  durationMs: number
  exitCode: number
  standardOutput: string
  standardError: string
  timedOut: boolean
  startError?: string | null
}

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  text: string
  timestamp: string
  action?: DiagnosisAction
  streaming?: boolean
  durationMs?: number
  error?: string
  model?: string
  aborted?: boolean
}

export interface AgentStatus {
  phase: string
  title: string
  description: string
  startedAt: number
}

export interface DiagnosisCase {
  name: string
  status: string
}
