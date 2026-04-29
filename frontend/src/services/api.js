import axios from 'axios'
import useAuthStore from '../stores/useAuthStore'

// Gateway API Axios instance – baseURL points at Gateway.API (:5000)
export const api = axios.create({
  baseURL: 'http://localhost:5000',
})

// ── AC5: attach Bearer token from useAuthStore on every request ──────────────
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// ── AC6: 401 → clear store + redirect to /login ──────────────────────────────
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout()
      window.location.href = '/login'
    }
    return Promise.reject(error)
  },
)

export default api
