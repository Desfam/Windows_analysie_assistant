import { MonitorCog } from 'lucide-react'
import type { Section } from '../../hooks/useSystemData'
import type { GpuInfo } from '../../types'
import { formatBytes, orNotAvailable } from '../../lib/format'
import { InfoRow } from '../common/InfoRow'
import { SectionCard } from './SectionCard'

interface Props {
  section: Section<GpuInfo[]>
  loading: boolean
  animate: boolean
}

export function GpuSection({ section, loading, animate }: Props) {
  const gpus = section.data ?? []

  return (
    <SectionCard
      title="Grafikkarte"
      icon={MonitorCog}
      status="NotChecked"
      loading={loading && section.data == null}
      failed={section.failed}
      animate={animate}
    >
      {gpus.length === 0 ? (
        <p className="text-sm italic text-slate-500">Keine Grafikkarte gefunden.</p>
      ) : (
        <div className="space-y-4">
          {gpus.map((gpu, index) => (
            <div key={`${gpu.name}-${index}`} className={index > 0 ? 'border-t border-white/[0.05] pt-3' : ''}>
              <p className="mb-1 text-sm font-medium text-slate-100">{orNotAvailable(gpu.name)}</p>
              <div className="divide-y divide-white/[0.04]">
                <InfoRow label="Hersteller" value={orNotAvailable(gpu.manufacturer)} />
                <InfoRow label="Treiberversion" value={orNotAvailable(gpu.driverVersion)} />
                <InfoRow label="Videospeicher" value={formatBytes(gpu.videoMemoryBytes)} />
              </div>
            </div>
          ))}
        </div>
      )}
    </SectionCard>
  )
}
