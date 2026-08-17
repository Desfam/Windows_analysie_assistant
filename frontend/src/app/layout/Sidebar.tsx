import { useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import {
  Activity,
  FolderKanban,
  LayoutDashboard,
  PanelLeftClose,
  PanelLeftOpen,
  RefreshCw,
  Settings,
  Sparkles,
  type LucideIcon
} from 'lucide-react'
import { useOllama } from '../ollama/OllamaContext'
import { ModelPicker } from './ModelPicker'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
}

const navItems: NavItem[] = [
  { to: '/overview', label: 'Systemübersicht', icon: LayoutDashboard },
  { to: '/diagnosis', label: 'KI-Diagnose', icon: Sparkles },
  { to: '/cases', label: 'Diagnosefälle', icon: FolderKanban },
  { to: '/settings', label: 'Einstellungen', icon: Settings }
]

const COLLAPSE_KEY = 'wda.sidebar.collapsed'

export function Sidebar() {
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(COLLAPSE_KEY) === '1')
  const { phase, status, refreshStatus } = useOllama()

  useEffect(() => {
    localStorage.setItem(COLLAPSE_KEY, collapsed ? '1' : '0')
  }, [collapsed])

  return (
    <aside
      className={`flex h-full shrink-0 flex-col border-r border-white/[0.06] bg-base-800/80 transition-[width] duration-200 ${
        collapsed ? 'w-[68px]' : 'w-[240px]'
      }`}
    >
      <div className="flex items-center gap-2.5 px-3 py-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-blue-500/15 text-blue-300">
          <Activity className="h-5 w-5" />
        </span>
        {!collapsed && (
          <span className="flex-1 truncate text-sm font-semibold text-slate-100">Windows Diagnose</span>
        )}
        <button
          type="button"
          onClick={() => setCollapsed((v) => !v)}
          className="rounded-lg p-1.5 text-slate-400 hover:bg-white/[0.06] hover:text-slate-200"
          aria-label={collapsed ? 'Seitenleiste ausklappen' : 'Seitenleiste einklappen'}
          title={collapsed ? 'Ausklappen' : 'Einklappen'}
        >
          {collapsed ? <PanelLeftOpen className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
        </button>
      </div>

      <nav className="flex-1 space-y-1 px-2 py-2">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            title={collapsed ? item.label : undefined}
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-lg px-2.5 py-2.5 text-sm font-medium transition-colors ${
                isActive
                  ? 'bg-blue-500/15 text-blue-200'
                  : 'text-slate-400 hover:bg-white/[0.05] hover:text-slate-200'
              } ${collapsed ? 'justify-center' : ''}`
            }
          >
            <item.icon className="h-5 w-5 shrink-0" />
            {!collapsed && <span className="truncate">{item.label}</span>}
          </NavLink>
        ))}
      </nav>

      <div className="space-y-2 border-t border-white/[0.06] p-2">
        <button
          type="button"
          onClick={() => void refreshStatus()}
          title={collapsed ? statusLabel(phase) : undefined}
          className={`flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-xs transition-colors hover:bg-white/[0.04] ${
            collapsed ? 'justify-center' : ''
          }`}
        >
          <StatusDot phase={phase} />
          {!collapsed && (
            <span className="min-w-0 flex-1 text-left">
              <span className="block truncate text-slate-300">{statusLabel(phase)}</span>
              {phase === 'connected' && status?.version && (
                <span className="block truncate text-[11px] text-slate-500">
                  Ollama {status.version}
                </span>
              )}
            </span>
          )}
          {!collapsed && <RefreshCw className="h-3.5 w-3.5 text-slate-500" />}
        </button>

        <ModelPicker collapsed={collapsed} />
      </div>
    </aside>
  )
}

function StatusDot({ phase }: { phase: string }) {
  const color =
    phase === 'connected' ? 'bg-emerald-400' : phase === 'checking' ? 'bg-amber-400' : 'bg-red-400'
  return (
    <span className="relative flex h-2.5 w-2.5 shrink-0">
      {phase === 'checking' && (
        <span className={`absolute inline-flex h-full w-full animate-ping rounded-full ${color} opacity-60`} />
      )}
      <span className={`relative inline-flex h-2.5 w-2.5 rounded-full ${color}`} />
    </span>
  )
}

function statusLabel(phase: string): string {
  switch (phase) {
    case 'connected':
      return 'Ollama verbunden'
    case 'checking':
      return 'Verbindung wird geprüft'
    default:
      return 'Ollama nicht erreichbar'
  }
}
