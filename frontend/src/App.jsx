import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import Layout          from './components/Layout'
import ProtectedRoute  from './components/ProtectedRoute'
import LoginPage       from './pages/LoginPage'
import DashboardPage   from './pages/DashboardPage'
import RoutesPage      from './pages/RoutesPage'
import LogsPage        from './pages/LogsPage'
import MetricsPage     from './pages/MetricsPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public */}
        <Route path="/login" element={<LoginPage />} />

        {/* Protected – Layout wraps all child routes via <Outlet /> */}
        <Route
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/routes"    element={<RoutesPage />} />
          <Route path="/logs"      element={<LogsPage />} />
          <Route path="/metrics"   element={<MetricsPage />} />
        </Route>

        {/* Catch-all → /dashboard (ProtectedRoute will redirect to /login if not authed) */}
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
