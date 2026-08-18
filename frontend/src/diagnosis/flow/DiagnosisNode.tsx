import { memo } from 'react'
import { Handle, Position, type NodeProps } from '@xyflow/react'
import { motion } from 'framer-motion'
import { Loader2 } from 'lucide-react'
import type { DiagnosisNode as DiagnosisNodeType, NodeKind } from '../types'
import { kindStyles, stateStyles } from '../lib/styles'
import { StatusBadge } from '../components/StatusBadge'

function nodeWidth(kind: NodeKind): number {
  switch (kind) {
    case 'decision':
      return 220
    case 'evidence':
      return 230
    case 'problem':
    case 'completion':
      return 240
    default:
      return 230
  }
}

function DiagnosisNodeComponent({ data, selected }: NodeProps<DiagnosisNodeType>) {
  const kind = kindStyles[data.kind]
  const style = stateStyles[data.state]
  const Icon = kind.icon
  const isRunning = data.state === 'running'

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: data.state === 'skipped' ? 0.45 : data.state === 'pending' ? 0.58 : 1, scale: 1 }}
      transition={{ duration: 0.3 }}
      style={{ width: nodeWidth(data.kind) }}
      className={`relative rounded-lg border bg-base-700/95 px-3.5 py-3 shadow-card transition-colors ${style.border} ${
        selected ? 'ring-2 ring-blue-400/70' : ''
      } ${data.state === 'pending' || data.state === 'skipped' ? 'border-dashed' : ''}`}
    >
      {isRunning && (
        <motion.span
          className="pointer-events-none absolute inset-0 rounded-xl ring-2 ring-blue-400/70"
          animate={{ opacity: [0.25, 0.8, 0.25] }}
          transition={{ duration: 1.6, repeat: Infinity, ease: 'easeInOut' }}
          aria-hidden
        />
      )}

      <Handle type="target" position={Position.Top} className="!h-2 !w-2 !border-0 !bg-slate-500" />

      <div className="flex items-start gap-2.5">
        <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/[0.05] ${kind.accent}`}>
          {isRunning ? <Loader2 className="h-4 w-4 animate-spin" /> : <Icon className="h-4 w-4" />}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1.5">
            <span className={`text-[10px] font-medium uppercase tracking-wide ${kind.accent}`}>
              {kind.label}
            </span>
          </div>
          <h3 className="truncate text-sm font-semibold text-slate-100">{data.title}</h3>
          <p className="mt-0.5 line-clamp-2 text-xs text-slate-400">{data.description}</p>
        </div>
      </div>

      <div className="mt-2.5 flex items-center gap-2">
        <StatusBadge state={data.state} />
        {data.estimatedDuration && (
          <span className="text-[10px] text-slate-500">{data.estimatedDuration}</span>
        )}
      </div>

      <Handle
        type="source"
        position={Position.Bottom}
        className="!h-2 !w-2 !border-0 !bg-slate-500"
      />
    </motion.div>
  )
}

export const DiagnosisNode = memo(DiagnosisNodeComponent)
