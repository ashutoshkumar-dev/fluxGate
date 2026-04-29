import { useCallback, useEffect, useState } from 'react'
import { logsService } from '../services/logsService'
import { cn } from '../lib/utils'

const PAGE_SIZE = 20

const STATUS_OPTIONS = [
  { value: '',      label: 'All levels' },
  { value: 'error', label: 'Error' },
  { value: 'warn',  label: 'Warning' },
  { value: 'info',  label: 'Information' },
]

const LEVEL_COLORS = {
  Error:       'text-red-600 bg-red-50',
  Warning:     'text-yellow-700 bg-yellow-50',
  Information: 'text-blue-600 bg-blue-50',
}

function LevelBadge({ level }) {
  return (
    <span className={cn('px-2 py-0.5 rounded text-xs font-medium', LEVEL_COLORS[level] ?? 'bg-muted text-muted-foreground')}>
      {level}
    </span>
  )
}

// ── Filters bar ───────────────────────────────────────────────────────────────
function LogsFilters({ filters, onChange, onReset }) {
  const inputCls = 'border border-input rounded-md px-3 py-1.5 text-sm bg-background focus:outline-none focus:ring-2 focus:ring-ring'

  return (
    <div className="flex flex-wrap gap-3 items-end">
      {/* AC4: status filter */}
      <div>
        <label className="block text-xs text-muted-foreground mb-1">Level</label>
        <select value={filters.status} onChange={(e) => onChange({ ...filters, status: e.target.value, page: 1 })} className={inputCls}>
          {STATUS_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>

      {/* AC5: route filter */}
      <div>
        <label className="block text-xs text-muted-foreground mb-1">Route</label>
        <input
          type="text"
          placeholder="/api/orders"
          value={filters.route}
          onChange={(e) => onChange({ ...filters, route: e.target.value, page: 1 })}
          className={cn(inputCls, 'w-40')}
        />
      </div>

      <div>
        <label className="block text-xs text-muted-foreground mb-1">From</label>
        <input type="datetime-local" value={filters.from} onChange={(e) => onChange({ ...filters, from: e.target.value, page: 1 })} className={inputCls} />
      </div>

      <div>
        <label className="block text-xs text-muted-foreground mb-1">To</label>
        <input type="datetime-local" value={filters.to} onChange={(e) => onChange({ ...filters, to: e.target.value, page: 1 })} className={inputCls} />
      </div>

      <button onClick={onReset} className="text-sm text-muted-foreground hover:text-foreground underline underline-offset-2">
        Reset
      </button>
    </div>
  )
}

// ── Logs table ────────────────────────────────────────────────────────────────
function LogsTable({ items }) {
  // AC7: empty state
  if (items.length === 0) {
    return (
      <div className="rounded-md border py-12 text-center">
        <p className="text-sm text-muted-foreground">No logs match the current filters.</p>
      </div>
    )
  }

  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full text-sm">
        <thead className="bg-muted/50">
          <tr>
            <th className="text-left px-3 py-2 font-medium whitespace-nowrap">Timestamp</th>
            <th className="text-left px-3 py-2 font-medium">Level</th>
            <th className="text-left px-3 py-2 font-medium">Route</th>
            <th className="text-left px-3 py-2 font-medium">Method</th>
            <th className="text-right px-3 py-2 font-medium">Status</th>
            <th className="text-right px-3 py-2 font-medium">Latency</th>
            <th className="text-left px-3 py-2 font-medium">TraceId</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {items.map((log, i) => (
            <tr key={log.traceId ?? i} className="hover:bg-muted/30 transition-colors">
              <td className="px-3 py-2 whitespace-nowrap text-xs text-muted-foreground">
                {new Date(log.timestamp).toLocaleString()}
              </td>
              <td className="px-3 py-2"><LevelBadge level={log.level} /></td>
              <td className="px-3 py-2 font-mono text-xs">{log.route ?? '—'}</td>
              <td className="px-3 py-2 text-xs">{log.method ?? '—'}</td>
              <td className="px-3 py-2 text-right">{log.statusCode ?? '—'}</td>
              <td className="px-3 py-2 text-right text-xs">
                {log.latencyMs != null ? `${log.latencyMs.toFixed(1)} ms` : '—'}
              </td>
              <td className="px-3 py-2 font-mono text-xs truncate max-w-[120px]" title={log.traceId}>
                {log.traceId ? log.traceId.slice(0, 8) + '…' : '—'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ── Pagination controls ───────────────────────────────────────────────────────
function Pagination({ page, totalPages, onChange }) {
  if (totalPages <= 1) return null
  return (
    <div className="flex items-center gap-2 justify-end text-sm">
      <button
        disabled={page <= 1}
        onClick={() => onChange(page - 1)}
        className="px-3 py-1.5 rounded-md border bg-background disabled:opacity-40 hover:bg-muted transition-colors"
      >
        ← Prev
      </button>
      <span className="text-muted-foreground">Page {page} of {totalPages}</span>
      <button
        disabled={page >= totalPages}
        onClick={() => onChange(page + 1)}  // AC6: page 2 loads next result set
        className="px-3 py-1.5 rounded-md border bg-background disabled:opacity-40 hover:bg-muted transition-colors"
      >
        Next →
      </button>
    </div>
  )
}

// ── LogsPage ──────────────────────────────────────────────────────────────────
const DEFAULT_FILTERS = { status: '', route: '', from: '', to: '', page: 1 }

export default function LogsPage() {
  const [filters,    setFilters]    = useState(DEFAULT_FILTERS)
  const [logPage,    setLogPage]    = useState(null)   // LogPageDto
  const [isLoading,  setIsLoading]  = useState(false)
  const [error,      setError]      = useState(null)

  const load = useCallback(async (f) => {
    setIsLoading(true)
    setError(null)
    try {
      const data = await logsService.getLogs({ ...f, pageSize: PAGE_SIZE })
      setLogPage(data)
    } catch (err) {
      setError(err.message ?? 'Failed to load logs')
    } finally {
      setIsLoading(false)
    }
  }, [])

  // AC3: load first page on mount
  useEffect(() => { load(filters) }, [])   // eslint-disable-line react-hooks/exhaustive-deps

  // Re-fetch whenever filters change (except initial mount — handled above)
  function applyFilters(next) {
    setFilters(next)
    load(next)
  }

  function handlePageChange(p) {
    const next = { ...filters, page: p }
    setFilters(next)
    load(next)
  }

  const totalPages = logPage ? Math.ceil(logPage.totalCount / PAGE_SIZE) : 0

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Logs</h1>
        <p className="text-sm text-muted-foreground">
          {logPage ? `${logPage.totalCount.toLocaleString()} entries` : 'Structured request logs from Seq'}
        </p>
      </div>

      <LogsFilters
        filters={filters}
        onChange={applyFilters}   // AC4, AC5
        onReset={() => applyFilters(DEFAULT_FILTERS)}
      />

      {error && (
        <div className="rounded-md border border-destructive bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground py-8 text-center">Loading logs…</p>
      ) : (
        <LogsTable items={logPage?.items ?? []} />  /* AC7: empty state inside LogsTable */
      )}

      <Pagination page={filters.page} totalPages={totalPages} onChange={handlePageChange} />
    </div>
  )
}
