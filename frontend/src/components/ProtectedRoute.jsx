import { Navigate } from 'react-router-dom'
import useAuthStore from '../stores/useAuthStore'

// Redirects unauthenticated users to /login (AC2).
export default function ProtectedRoute({ children }) {
  const token = useAuthStore((s) => s.token)
  if (!token) return <Navigate to="/login" replace />
  return children
}
