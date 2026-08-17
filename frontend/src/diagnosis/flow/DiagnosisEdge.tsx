import {
  BaseEdge,
  EdgeLabelRenderer,
  getSmoothStepPath,
  type EdgeProps
} from '@xyflow/react'
import type { DiagnosisEdge as DiagnosisEdgeType, EdgeState } from '../types'
import { usePrefersReducedMotion } from '../lib/styles'

const edgeColor: Record<EdgeState, string> = {
  pending: '#64748b',
  active: '#3b82f6',
  completed: '#10b981',
  discarded: '#475569'
}

export function DiagnosisEdge({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  data,
  markerEnd
}: EdgeProps<DiagnosisEdgeType>) {
  const [path, labelX, labelY] = getSmoothStepPath({
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    borderRadius: 12
  })

  const state: EdgeState = data?.state ?? 'pending'
  const color = edgeColor[state]
  const reducedMotion = usePrefersReducedMotion()
  const showPulse = state === 'active' && !reducedMotion

  return (
    <>
      <BaseEdge
        id={id}
        path={path}
        markerEnd={markerEnd}
        style={{
          stroke: color,
          strokeWidth: state === 'active' ? 2.5 : 1.5,
          strokeDasharray: state === 'discarded' ? '6 6' : undefined,
          opacity: state === 'discarded' ? 0.5 : 1
        }}
      />

      {showPulse && (
        <circle r={3.5} fill="#bfdbfe">
          <animateMotion dur="1.6s" repeatCount="indefinite" path={path} />
        </circle>
      )}

      {data?.label && (
        <EdgeLabelRenderer>
          <div
            style={{
              transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`
            }}
            className="pointer-events-none absolute rounded-md border border-white/[0.08] bg-base-800/90 px-2 py-0.5 text-[10px] font-medium text-slate-300"
          >
            {data.label}
          </div>
        </EdgeLabelRenderer>
      )}
    </>
  )
}
