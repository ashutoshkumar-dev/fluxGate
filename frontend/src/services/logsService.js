import api from './api'

// GET /logs?page=&pageSize=&status=&route=&from=&to=
export const logsService = {
  getLogs: (filters = {}) => {
    const params = new URLSearchParams()
    if (filters.page)     params.set('page',     filters.page)
    if (filters.pageSize) params.set('pageSize', filters.pageSize)
    if (filters.status)   params.set('status',   filters.status)
    if (filters.route)    params.set('route',    filters.route)
    if (filters.from)     params.set('from',     filters.from)
    if (filters.to)       params.set('to',       filters.to)
    return api.get(`/logs?${params.toString()}`).then((r) => r.data)
  },
}
