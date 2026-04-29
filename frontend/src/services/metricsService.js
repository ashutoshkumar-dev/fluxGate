import api from './api'

export const metricsService = {
  getSummary: () => api.get('/metrics/summary').then((r) => r.data),
}
