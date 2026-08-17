import dagre from 'dagre'
import type { DiagnosisEdge, DiagnosisNode, NodeKind } from '../types'

export type LayoutDirection = 'TB' | 'LR'

function nodeSize(kind: NodeKind): { width: number; height: number } {
  switch (kind) {
    case 'decision':
      return { width: 220, height: 88 }
    case 'evidence':
      return { width: 230, height: 84 }
    case 'problem':
    case 'completion':
      return { width: 240, height: 84 }
    default:
      return { width: 250, height: 96 }
  }
}

/**
 * Berechnet automatisch überschneidungsfreie Positionen für alle Knoten.
 * Die Knoten speichern keine festen Bildschirmkoordinaten; das Layout wird
 * bei jeder Graph-Änderung neu bestimmt.
 */
export function layoutGraph(
  nodes: DiagnosisNode[],
  edges: DiagnosisEdge[],
  direction: LayoutDirection = 'TB'
): DiagnosisNode[] {
  const graph = new dagre.graphlib.Graph()
  graph.setDefaultEdgeLabel(() => ({}))
  graph.setGraph({
    rankdir: direction,
    nodesep: 48,
    ranksep: 64,
    marginx: 24,
    marginy: 24
  })

  for (const node of nodes) {
    const { width, height } = nodeSize(node.data.kind)
    graph.setNode(node.id, { width, height })
  }

  for (const edge of edges) {
    graph.setEdge(edge.source, edge.target)
  }

  dagre.layout(graph)

  return nodes.map((node) => {
    const layouted = graph.node(node.id)
    const { width, height } = nodeSize(node.data.kind)
    return {
      ...node,
      position: {
        x: layouted.x - width / 2,
        y: layouted.y - height / 2
      }
    }
  })
}
