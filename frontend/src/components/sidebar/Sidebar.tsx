import type { SystemData } from '../../hooks/useSystemData'
import { CpuSection } from './CpuSection'
import { DiskSection } from './DiskSection'
import { GeneralSection } from './GeneralSection'
import { GpuSection } from './GpuSection'
import { MemorySection } from './MemorySection'
import { WindowsSection } from './WindowsSection'

interface SidebarProps {
  data: SystemData
  loading: boolean
  animate: boolean
}

export function Sidebar({ data, loading, animate }: SidebarProps) {
  return (
    <aside className="flex flex-col gap-3">
      <GeneralSection section={data.summary} loading={loading} animate={animate} />
      <CpuSection section={data.cpu} loading={loading} animate={animate} />
      <MemorySection section={data.memory} loading={loading} animate={animate} />
      <GpuSection section={data.gpus} loading={loading} animate={animate} />
      <DiskSection section={data.disks} loading={loading} animate={animate} />
      <WindowsSection section={data.windows} loading={loading} animate={animate} />
    </aside>
  )
}
