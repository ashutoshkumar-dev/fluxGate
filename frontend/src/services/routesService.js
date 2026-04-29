import api from './api'

// All routes live under /gateway/routes (Phase 2 API)
const BASE = '/gateway/routes'

export const routesService = {
  getRoutes:    ()           => api.get(BASE).then((r) => r.data),
  createRoute:  (data)       => api.post(BASE, data).then((r) => r.data),
  updateRoute:  (id, data)   => api.put(`${BASE}/${id}`, data).then((r) => r.data),
  deleteRoute:  (id)         => api.delete(`${BASE}/${id}`),
}
