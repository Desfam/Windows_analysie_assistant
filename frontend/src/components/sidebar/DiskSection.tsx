import { HardDrive } from 'lucide-react'
import type { Section } from '../../hooks/useSystemData'
import type { DiskInfo, HealthStatus } from '../../types'
import { formatBytes, formatPercent, orNotAvailable } from '../../lib/format'
import { ProgressBar } from '../common/ProgressBar'
import { StatusIndicator } from '../common/StatusIndicator'
import { SectionCard } from './SectionCard'

interface Props {
  section: Section<DiskInfo[]>
  loading: boolean
  animate: boolean
}

function aggregateStatus(disks: DiskInfo[]): HealthStatus {
  if (disks.some((disk) => disk.status === 'Critical')) return 'Critical'
  if (disks.some((disk) => disk.status === 'Warning')) return 'Warning'
  if (disks.length === 0) return 'NotChecked'
  return 'Normal'
}

export function DiskSection({ section, loading, animate }: Props) {
  const disks = section.data ?? []

  return (
    <SectionCard
      title="Datenträger"
      icon={HardDrive}
      status={aggregateStatus(disks)}
      loading={loading && section.data == null}
      failed={section.failed}
      animate={animate}
    >
      {disks.length === 0 ? (
        <p className="text-sm italic text-slate-500">Keine lokalen Laufwerke gefunden.</p>
      ) : (
        <div className="space-y-4">
          {disks.map((disk) => (
            <div key={disk.driveLetter}>
              <div className="mb-1.5 flex items-center justify-between">
                <span className="text-sm font-medium text-slate-100">
                  {orNotAvailable(disk.driveLetter)}
                  <span className="ml-2 text-xs text-slate-500">{orNotAvailable(disk.fileSystem)}</span>
                </span>
                <StatusIndicator status={disk.status} />
              </div>
              <ProgressBar percent={disk.usagePercent} animate={animate} />
              <div className="mt-1.5 flex items-center justify-between text-xs text-slate-400">
                <span>
                  {formatBytes(disk.usedBytes)} von {formatBytes(disk.totalBytes)}
                </span>
                <span>{formatPercent(disk.usagePercent)}</span>
              </div>
              <div className="text-xs text-slate-500">{formatBytes(disk.freeBytes)} frei</div>
            </div>
          ))}
        </div>
      )}
    </SectionCard>
  )
}
