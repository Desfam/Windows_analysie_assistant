import { FlaskConical } from 'lucide-react'
import type { DiagnosisNode } from '../types'

interface EvidenceListProps {
  nodes: DiagnosisNode[]
  onSelect: (id: string) => void
}

export function EvidenceList({ nodes, onSelect }: EvidenceListProps) {
  const evidenceNodes = nodes.filter((node) => node.data.kind === 'evidence')

  return (
    <div>
      <h4 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
        Bisherige Belege
      </h4>
      {evidenceNodes.length === 0 ? (
        <p className="rounded-lg border border-dashed border-white/[0.08] px-3 py-3 text-xs text-slate-500">
          Noch keine Belege gesammelt. Führe einen Diagnoseschritt aus.
        </p>
      ) : (
        <ul className="space-y-1.5">
          {evidenceNodes.map((node) => (
            <li key={node.id}>
              <button
                type="button"
                onClick={() => onSelect(node.id)}
                className="flex w-full items-start gap-2.5 rounded-lg border border-white/[0.06] bg-base-800/50 px-3 py-2 text-left transition-colors hover:border-emerald-500/30 hover:bg-emerald-500/[0.05]"
              >
                <span className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-emerald-500/15 text-emerald-300">
                  <FlaskConical className="h-4 w-4" />
                </span>
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-slate-100">
                    {node.data.title}
                  </span>
                  <span className="block text-xs text-slate-400">{node.data.description}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
