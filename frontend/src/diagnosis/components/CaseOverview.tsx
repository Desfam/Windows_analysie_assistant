import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { ChevronDown, Network, Sparkles } from 'lucide-react'
import type { Cause, DiagnosisCase, DiagnosisEdge, DiagnosisNode } from '../types'
import { DiagnosisFlow } from '../flow/DiagnosisFlow'
import { CauseList } from './CauseList'
import { EvidenceList } from './EvidenceList'
import { NodeDetailPanel } from './NodeDetailPanel'

interface CaseOverviewProps {
  caseInfo: DiagnosisCase
  nodes: DiagnosisNode[]
  edges: DiagnosisEdge[]
  causes: Cause[]
  selectedNode: DiagnosisNode | null
  onSelectNode: (id: string | null) => void
}

const legend = [
  { label: 'Abgeschlossen', dot: 'bg-emerald-400' },
  { label: 'Wird ausgeführt', dot: 'bg-blue-400' },
  { label: 'Wartet', dot: 'bg-slate-500' },
  { label: 'Bestätigung nötig', dot: 'bg-orange-400' }
]

export function CaseOverview({
  caseInfo,
  nodes,
  edges,
  causes,
  selectedNode,
  onSelectNode
}: CaseOverviewProps) {
  const [panelOpen, setPanelOpen] = useState(true)

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-start justify-between gap-3 border-b border-white/[0.06] px-5 py-4">
        <div className="flex items-start gap-3">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-violet-500/15 text-violet-300">
            <Network className="h-5 w-5" />
          </span>
          <div>
            <h2 className="text-base font-semibold text-slate-100">Diagnoseablauf</h2>
            <p className="text-sm text-slate-500">{caseInfo.name}</p>
          </div>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-lg border border-blue-500/30 bg-blue-500/10 px-3 py-1.5 text-xs font-medium text-blue-200">
          <Sparkles className="h-3.5 w-3.5" />
          {caseInfo.status}
        </span>
      </header>

      <div className="relative min-h-0 flex-1">
        <div className="absolute inset-0">
          <DiagnosisFlow
            nodes={nodes}
            edges={edges}
            selectedNodeId={selectedNode?.id ?? null}
            onSelect={onSelectNode}
          />
        </div>
        <div className="pointer-events-none absolute bottom-3 left-3 flex flex-wrap gap-3 rounded-lg border border-white/[0.06] bg-base-800/80 px-3 py-1.5 backdrop-blur">
          {legend.map((item) => (
            <span key={item.label} className="flex items-center gap-1.5 text-[11px] text-slate-400">
              <span className={`h-2 w-2 rounded-full ${item.dot}`} />
              {item.label}
            </span>
          ))}
        </div>
      </div>

      <div className="border-t border-white/[0.06]">
        <button
          type="button"
          onClick={() => setPanelOpen((value) => !value)}
          className="flex w-full items-center gap-2 px-5 py-2.5 text-left text-xs font-medium text-slate-400 hover:text-slate-200"
        >
          <ChevronDown className={`h-4 w-4 transition-transform ${panelOpen ? '' : '-rotate-90'}`} />
          {selectedNode ? 'Knoten-Detail' : 'Ursachen & Belege'}
        </button>

        <AnimatePresence initial={false}>
          {panelOpen && (
            <motion.div
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              transition={{ duration: 0.2 }}
              className="overflow-hidden"
            >
              <div className="max-h-72 overflow-y-auto px-5 pb-4">
                {selectedNode ? (
                  <NodeDetailPanel node={selectedNode} onClose={() => onSelectNode(null)} />
                ) : (
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                    <CauseList causes={causes} />
                    <EvidenceList nodes={nodes} onSelect={onSelectNode} />
                  </div>
                )}
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  )
}
