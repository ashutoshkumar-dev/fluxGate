import { create } from 'zustand'
import { metricsService } from '../services/metricsService'

const POLL_INTERVAL_MS = 30_000  // AC2: 30s auto-poll

const useMetricsStore = create((set, get) => ({
  summary:    null,
  // history kept for charts: array of { time, totalRequests, errorRate, avgLatencyMs }
  history:    [],
  isLoading:  false,
  error:      null,
  _timerId:   null,

  fetchSummary: async () => {
    set({ isLoading: true, error: null })
    try {
      const summary = await metricsService.getSummary()
      const point = {
        time:           new Date().toLocaleTimeString(),
        totalRequests:  summary.totalRequests,
        errorRate:      parseFloat((summary.errorRate * 100).toFixed(2)),
        avgLatencyMs:   parseFloat(summary.avgLatencyMs.toFixed(1)),
      }
      set((s) => ({
        summary,
        history:   [...s.history.slice(-19), point],  // keep last 20 points
        isLoading: false,
      }))
    } catch (err) {
      set({ isLoading: false, error: err.message ?? 'Failed to load metrics' })
    }
  },

  // Start 30s polling – idempotent (won't start a second timer)
  startPolling: () => {
    const { _timerId, fetchSummary } = get()
    if (_timerId) return
    fetchSummary()  // immediate first fetch
    const id = setInterval(fetchSummary, POLL_INTERVAL_MS)
    set({ _timerId: id })
  },

  stopPolling: () => {
    const { _timerId } = get()
    if (_timerId) { clearInterval(_timerId); set({ _timerId: null }) }
  },
}))

export default useMetricsStore
