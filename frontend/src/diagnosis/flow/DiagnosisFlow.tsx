import { useEffect, useMemo, useRef } from 'react'
import {
  Background,
  BackgroundVariant,
  Controls,
  MarkerType,
  ReactFlow,
  ReactFlowProvider,
  useReactFlow,
  type NodeMouseHandler
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import type { DiagnosisEdge, DiagnosisNode } from '../types'
import { layoutGraph, type LayoutDirection } from '../layout/autoLayout'
import { DiagnosisNode as DiagnosisNodeView } from './DiagnosisNode'
import { DiagnosisEdge as DiagnosisEdgeView } from './DiagnosisEdge'

const nodeTypes = { diagnosis: DiagnosisNodeView }
const edgeTypes = { diagnosis: DiagnosisEdgeView }

const defaultEdgeOptions = {
  type: 'diagnosis',
  markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color: '#64748b' }
}

interface DiagnosisFlowProps {
  nodes: DiagnosisNode[]
  edges: DiagnosisEdge[]
  selectedNodeId: string | null
  direction?: LayoutDirection
  onSelect: (id: string | null) => void
}

function FlowInner({ nodes, edges, selectedNodeId, direction = 'TB', onSelect }: DiagnosisFlowProps) {
  const { fitView } = useReactFlow()
  const previousCount = useRef(0)
  const graphNodes = useMemo(() => nodes.filter((node) => node.data.kind !== 'evidence'), [nodes])
  const graphNodeIds = useMemo(() => new Set(graphNodes.map((node) => node.id)), [graphNodes])
  const graphEdges = useMemo(
    () => edges.filter((edge) => graphNodeIds.has(edge.source) && graphNodeIds.has(edge.target)),
    [edges, graphNodeIds]
  )

  const layoutedNodes = useMemo(() => {
    const laidOut = layoutGraph(graphNodes, graphEdges, direction)
    return laidOut.map((node) => ({ ...node, selected: node.id === selectedNodeId }))
  }, [graphNodes, graphEdges, direction, selectedNodeId])

  useEffect(() => {
    if (layoutedNodes.length !== previousCount.current) {
      previousCount.current = layoutedNodes.length
      const timer = window.setTimeout(() => fitView({ padding: 0.45, duration: 400, maxZoom: 0.95 }), 60)
      return () => window.clearTimeout(timer)
    }
    return undefined
  }, [layoutedNodes.length, fitView])

  const handleNodeClick: NodeMouseHandler = (_, node) => onSelect(node.id)

  return (
    <ReactFlow
      nodes={layoutedNodes}
      edges={graphEdges}
      nodeTypes={nodeTypes}
      edgeTypes={edgeTypes}
      defaultEdgeOptions={defaultEdgeOptions}
      nodesDraggable={false}
      nodesConnectable={false}
      elementsSelectable
      onNodeClick={handleNodeClick}
      onPaneClick={() => onSelect(null)}
      fitView
      minZoom={0.55}
      maxZoom={1.6}
      proOptions={{ hideAttribution: true }}
      className="bg-base-900"
    >
      <Background variant={BackgroundVariant.Dots} gap={22} size={1} color="#243040" />
      <Controls
        showInteractive={false}
        className="!border-white/10 !bg-base-800/90 [&_button]:!border-white/10 [&_button]:!bg-base-700 [&_button]:!text-slate-300 [&_button:hover]:!bg-base-600"
      />
    </ReactFlow>
  )
}

export function DiagnosisFlow(props: DiagnosisFlowProps) {
  return (
    <ReactFlowProvider>
      <FlowInner {...props} />
    </ReactFlowProvider>
  )
}
