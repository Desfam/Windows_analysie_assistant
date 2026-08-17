import { z } from 'zod'
import type { DiagnosisNodeData, ExecutionState, NodeKind, RiskLevel } from '../../diagnosis/types'
import type { SeedNode } from '../cases/caseFactory'

/**
 * Schema für strukturierte, vom Modell vorgeschlagene Planänderungen.
 *
 * WICHTIG: Diese Funktion ist vorbereitet, aber bewusst NICHT automatisch mit dem
 * Chat verbunden. Freier Modelltext darf den Graphen niemals ungeprüft verändern.
 * Erst nach Validierung und Bereinigung dürfen Planänderungen angewendet werden.
 */
const planNodeSchema = z.object({
  id: z.string().min(1).max(120),
  type: z.enum(['question', 'hypothesis', 'check', 'evidence', 'repair', 'followup', 'decision']),
  title: z.string().min(1).max(200),
  description: z.string().max(1000).optional().default(''),
  status: z
    .enum([
      'pending',
      'ready',
      'running',
      'completed',
      'failed',
      'skipped',
      'waitingForApproval',
      'waitingForUser',
      'blocked'
    ])
    .optional()
    .default('ready'),
  riskLevel: z.enum(['none', 'low', 'medium', 'high']).optional().default('none'),
  changesSystem: z.boolean().optional().default(false)
})

const graphChangeSchema = z.object({
  operation: z.enum(['addNode', 'updateNode']),
  node: planNodeSchema
})

export const graphPlanSchema = z.object({
  summary: z.string().max(2000),
  graphChanges: z.array(graphChangeSchema).max(20).optional().default([])
})

export type GraphPlan = z.infer<typeof graphPlanSchema>
export type PlanNode = z.infer<typeof planNodeSchema>

const kindMap: Record<PlanNode['type'], NodeKind> = {
  question: 'userQuery',
  hypothesis: 'decision',
  check: 'action',
  evidence: 'evidence',
  repair: 'repair',
  followup: 'verification',
  decision: 'decision'
}

const riskMap: Record<PlanNode['riskLevel'], RiskLevel> = {
  none: 'R0',
  low: 'R1',
  medium: 'R2',
  high: 'R3'
}

export function parseGraphPlan(raw: unknown): { ok: true; plan: GraphPlan } | { ok: false; error: string } {
  const result = graphPlanSchema.safeParse(raw)
  if (!result.success) {
    return { ok: false, error: 'Der strukturierte Plan ist ungültig und wird verworfen.' }
  }
  return { ok: true, plan: result.data }
}

/**
 * Wandelt validierte Planknoten in interne, sichere Knoten um.
 * Sicherheitsregeln:
 *  - Modell darf keinen Knoten als „completed“ markieren (kein reales Ergebnis).
 *  - Belege bleiben unbestätigte Kandidaten (Status „pending“).
 *  - Reparaturknoten erfordern immer eine ausdrückliche Bestätigung.
 */
export function planToSeedNodes(plan: GraphPlan): SeedNode[] {
  return plan.graphChanges
    .filter((change) => change.operation === 'addNode')
    .map((change) => sanitizeNode(change.node))
}

function sanitizeNode(node: PlanNode): SeedNode {
  const kind = kindMap[node.type]
  let state = clampState(node.status)
  const requiresApproval = kind === 'repair'

  if (kind === 'evidence') {
    state = 'pending'
  }
  if (requiresApproval && (state === 'running' || state === 'completed')) {
    state = 'waitingForApproval'
  }

  const data: DiagnosisNodeData = {
    kind,
    title: node.title,
    description: node.description,
    state,
    risk: riskMap[node.riskLevel],
    systemImpact: {
      changesSystem: node.changesSystem && requiresApproval,
      label: node.changesSystem ? 'Systemänderung möglich – Bestätigung nötig' : 'Keine Systemänderung'
    },
    requiresApproval: requiresApproval ? true : undefined
  }

  return { id: node.id, data }
}

function clampState(state: ExecutionState): ExecutionState {
  // Das Modell darf nichts als abgeschlossen ausgeben – es liegt kein reales Ergebnis vor.
  if (state === 'completed' || state === 'running') {
    return 'ready'
  }
  return state
}
