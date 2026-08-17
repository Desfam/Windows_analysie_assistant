import type {
  ChatMessage,
  Cause,
  DiagnosisCase,
  DiagnosisAction,
  DiagnosisNodeData,
  Evidence
} from '../types'

const readOnlyImpact = { changesSystem: false, label: 'Keine Systemänderung' }

export const initialCase: DiagnosisCase = {
  name: 'Unregelmäßige System-Freezes',
  status: 'Untersuchung läuft'
}

export const eventsAction: DiagnosisAction = {
  id: 'action-events',
  title: 'Windows-Ereignisse untersuchen',
  description: 'Ereignisprotokolle nach Fehlern und Warnungen durchsuchen.',
  systemImpact: readOnlyImpact,
  risk: 'R1',
  estimatedDuration: 'ca. 10 Sekunden',
  note: 'Es werden nur Protokolle gelesen. Am System wird nichts verändert.',
  demoCommand:
    "Get-WinEvent -FilterHashtable @{ LogName='System'; Level=1,2,3;\n" +
    '  StartTime=(Get-Date).AddHours(-24) } |\n' +
    '  Where-Object { $_.Id -in 41,1001,129,6008 } |\n' +
    '  Select-Object TimeCreated, Id, ProviderName, LevelDisplayName',
  state: 'ready',
  targetNodeId: 'events'
}

export const initialChat: ChatMessage[] = [
  {
    id: 'msg-1',
    role: 'user',
    text: 'Mein Rechner friert seit einigen Tagen unregelmäßig ein.',
    timestamp: '14:32'
  },
  {
    id: 'msg-2',
    role: 'assistant',
    text:
      'Verstanden. Ich sammle zunächst Belege aus dem System, um die Ursache einzugrenzen. ' +
      'Als Erstes untersuchen wir die Windows-Ereignisse, insbesondere Kernel-, Treiber- und Speicherfehler.',
    timestamp: '14:32',
    action: eventsAction
  }
]

export const initialCauses: Cause[] = [
  { id: 'cause-nvme', title: 'NVMe-Treiber oder Firmware', level: 'unclear' },
  { id: 'cause-ram', title: 'Arbeitsspeicher', level: 'unclear' },
  { id: 'cause-update', title: 'Windows Update', level: 'unclear' }
]

export const evidence129: Evidence = {
  id: 'ev-129',
  eventId: 129,
  source: 'stornvme',
  summary: '3 Treffer vor erzwungenen Neustarts'
}

interface SeedNode {
  id: string
  data: DiagnosisNodeData
}

export const initialNodes: SeedNode[] = [
  {
    id: 'problem',
    data: {
      kind: 'problem',
      title: 'Problem aufgenommen',
      description: 'Symptom erfasst und Fall angelegt',
      state: 'completed',
      risk: 'R0',
      systemImpact: readOnlyImpact,
      reason:
        'Der Benutzer meldet unregelmäßige System-Freezes. Der Fall wurde angelegt, ' +
        'um die Ursache systematisch einzugrenzen.',
      result: 'Fall „Unregelmäßige System-Freezes“ angelegt.'
    }
  },
  {
    id: 'events',
    data: {
      kind: 'action',
      title: 'Ereignisprotokolle prüfen',
      description: 'Windows-Ereignisse werden untersucht',
      state: 'ready',
      risk: 'R1',
      systemImpact: readOnlyImpact,
      estimatedDuration: 'ca. 10 Sekunden',
      demoCommand: eventsAction.demoCommand,
      reason:
        'Freezes hinterlassen häufig Spuren in den Windows-Ereignissen (z. B. Kernel-, ' +
        'Treiber- oder Speicherfehler). Diese Belege grenzen die Ursache ein.',
      nextSteps: ['Belege auswerten', 'Hinweise bewerten']
    }
  }
]

export const initialEdges = [{ id: 'e-problem-events', source: 'problem', target: 'events' }]

/**
 * Erzeugt die nach der Ausführung von „Ereignisprotokolle prüfen“ dynamisch
 * hinzukommenden Knoten und Verbindungen sowie die Chat-Zusammenfassung.
 */
export function buildEventsFollowUp() {
  const nodes: SeedNode[] = [
    {
      id: 'evidence-129',
      data: {
        kind: 'evidence',
        title: 'Ereignis 129 · stornvme',
        description: '3 Treffer vor erzwungenen Neustarts',
        state: 'completed',
        risk: 'R0',
        systemImpact: readOnlyImpact,
        evidence: [evidence129],
        result: 'Der NVMe-Treiber musste den Controller mehrfach zurücksetzen.',
        reason:
          'Ereignis 129 (stornvme) tritt kurz vor erzwungenen Neustarts auf und deutet ' +
          'auf ein Problem mit dem NVMe-Laufwerk oder dessen Treiber hin.'
      }
    },
    {
      id: 'decision-hints',
      data: {
        kind: 'decision',
        title: 'Hinweise gefunden?',
        description: 'Bewertung der gefundenen Belege',
        state: 'completed',
        risk: 'R0',
        systemImpact: readOnlyImpact,
        condition: 'Wurden relevante Ereignisse gefunden?',
        result: 'Starke Hinweise auf NVMe-bezogene Fehler.'
      }
    },
    {
      id: 'nvme',
      data: {
        kind: 'action',
        title: 'NVMe-Zustand prüfen',
        description: 'SMART-Werte und Treiberzustand auslesen',
        state: 'pending',
        risk: 'R1',
        systemImpact: readOnlyImpact,
        estimatedDuration: 'ca. 15 Sekunden',
        demoCommand:
          'Get-PhysicalDisk | Get-StorageReliabilityCounter |\n' +
          '  Select-Object DeviceId, Wear, ReadErrorsTotal, WriteErrorsTotal',
        reason:
          'Da Ereignis 129 auf das NVMe-Laufwerk zeigt, wird dessen Zustand ' +
          '(SMART-Werte, Treiberversion) genauer geprüft.'
      }
    },
    {
      id: 'evaluate',
      data: {
        kind: 'verification',
        title: 'Ursache bewerten',
        description: 'Belege zusammenführen und Ursache eingrenzen',
        state: 'pending',
        risk: 'R0',
        systemImpact: readOnlyImpact,
        estimatedDuration: 'ca. 5 Sekunden',
        reason: 'Die gesammelten Belege werden zu einer belastbaren Ursachenbewertung verdichtet.'
      }
    },
    {
      id: 'memory',
      data: {
        kind: 'action',
        title: 'Arbeitsspeicher prüfen',
        description: 'Alternativer Pfad – aktuell nicht verfolgt',
        state: 'skipped',
        risk: 'R1',
        systemImpact: readOnlyImpact,
        estimatedDuration: 'ca. 20 Sekunden',
        reason:
          'Der Arbeitsspeicher wäre der nächste Prüfschritt, falls keine NVMe-Hinweise ' +
          'vorlägen. Aufgrund der starken NVMe-Hinweise wird dieser Pfad vorerst nicht verfolgt.'
      }
    }
  ]

  const edges = [
    { id: 'e-events-ev129', source: 'events', target: 'evidence-129', label: undefined },
    { id: 'e-ev129-decision', source: 'evidence-129', target: 'decision-hints', label: undefined },
    { id: 'e-decision-nvme', source: 'decision-hints', target: 'nvme', label: 'Starke Hinweise' },
    { id: 'e-nvme-evaluate', source: 'nvme', target: 'evaluate', label: 'Prüfung erfolgreich' },
    {
      id: 'e-decision-memory',
      source: 'decision-hints',
      target: 'memory',
      label: 'Keine passenden Ereignisse'
    }
  ]

  const summary =
    'Auswertung abgeschlossen: In den letzten 24 Stunden trat dreimal das Ereignis 129 ' +
    '(Quelle: stornvme) unmittelbar vor erzwungenen Neustarts auf. Das deutet stark auf ein ' +
    'NVMe-Treiber- oder Firmware-Problem hin. Als Nächstes prüfen wir den NVMe-Zustand.'

  return { nodes, edges, readyNodeId: 'nvme', summary }
}
