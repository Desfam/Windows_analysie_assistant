import type { Node, Edge } from '@xyflow/react'

export type ExecutionState =
  | 'pending'
  | 'ready'
  | 'running'
  | 'completed'
  | 'failed'
  | 'skipped'
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
  demoCommand: string
  state: ExecutionState
  targetNodeId: string
}

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  text: string
  timestamp: string
  action?: DiagnosisAction
}

export interface DiagnosisCase {
  name: string
  status: string
}
