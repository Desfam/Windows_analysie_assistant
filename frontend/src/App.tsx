import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { OllamaProvider } from './app/ollama/OllamaContext'
import { CasesProvider } from './app/cases/CasesContext'
import { AppLayout } from './app/layout/AppLayout'
import { OverviewPage } from './app/pages/OverviewPage'
import { KiDiagnosisPage } from './app/pages/KiDiagnosisPage'
import { CasesPage } from './app/pages/CasesPage'
import { SettingsPage } from './app/pages/SettingsPage'

export default function App() {
  return (
    <OllamaProvider>
      <CasesProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<AppLayout />}>
              <Route index element={<Navigate to="/overview" replace />} />
              <Route path="/overview" element={<OverviewPage />} />
              <Route path="/diagnosis" element={<KiDiagnosisPage />} />
              <Route path="/cases" element={<CasesPage />} />
              <Route path="/settings" element={<SettingsPage />} />
              <Route path="*" element={<Navigate to="/overview" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </CasesProvider>
    </OllamaProvider>
  )
}
