import type {
  DiagnosisEdge,
  DiagnosisNode,
  DiagnosisNodeData,
  EdgeState
} from '../../diagnosis/types'
import type { DiagnosisCase } from './casesTypes'

export interface SeedNode {
  id: string
  data: DiagnosisNodeData
}

export interface SeedEdge {
  id: string
  source: string
  target: string
  label?: string
}

export function toNode(seed: SeedNode): DiagnosisNode {
  return { id: seed.id, type: 'diagnosis', position: { x: 0, y: 0 }, data: seed.data }
}

export function toEdge(seed: SeedEdge, state: EdgeState): DiagnosisEdge {
  return {
    id: seed.id,
    source: seed.source,
    target: seed.target,
    type: 'diagnosis',
    data: { label: seed.label, state }
  }
}

let caseCounter = 0

export function nextCaseId(): string {
  caseCounter += 1
  return `case-${Date.now()}-${caseCounter}`
}

export function createEmptyCase(title: string, modelName: string | null): DiagnosisCase {
  const now = new Date().toISOString()
  return {
    id: nextCaseId(),
    title,
    createdAt: now,
    updatedAt: now,
    status: 'open',
    modelName,
    messages: [],
    nodes: [],
    edges: [],
    causes: [],
    evidence: [],
    selectedEventIds: [],
    isDemo: false
  }
}
