import type {
  ChatMessage,
  Cause,
  DiagnosisEdge,
  DiagnosisNode,
  Evidence
} from '../../diagnosis/types'

export type CaseStatus = 'open' | 'running' | 'waiting' | 'resolved' | 'closed'

export interface DiagnosisCase {
  id: string
  title: string
  createdAt: string
  updatedAt: string
  status: CaseStatus
  modelName: string | null
  messages: ChatMessage[]
  nodes: DiagnosisNode[]
  edges: DiagnosisEdge[]
  causes: Cause[]
  evidence: Evidence[]
  selectedEventIds: string[]
  isDemo?: boolean
}
