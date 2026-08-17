import { useNavigate } from 'react-router-dom'
import { DashboardPage } from '../../pages/DashboardPage'
import { useCases } from '../cases/CasesContext'
import type { EventItem } from '../../types'

export function OverviewPage() {
  const navigate = useNavigate()
  const { addEventCandidate } = useCases()

  const handleInvestigate = (event: EventItem) => {
    addEventCandidate(event)
    navigate('/diagnosis')
  }

  return <DashboardPage onInvestigate={handleInvestigate} />
}
