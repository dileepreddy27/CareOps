import { useCallback, useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import {
  Activity, AlertTriangle, Bell, CalendarDays, Check, ChevronRight, ClipboardCheck,
  Clock3, FileCheck2, HeartPulse, LayoutDashboard, LogOut, Menu, MessageSquare,
  RefreshCw, Search, ShieldCheck, Users, X,
} from 'lucide-react'
import { api } from './api'
import type {
  Dashboard, PageResult, ProviderDetail, ProviderSummary, Session, Shift, WorkflowStatus,
} from './types'

const SESSION_KEY = 'careops.session'
const DEMO_PASSWORD = 'CareOps-Demo-2026!'

function App() {
  const [session, setSession] = useState<Session | null>(() => {
    const stored = localStorage.getItem(SESSION_KEY)
    return stored ? JSON.parse(stored) as Session : null
  })

  const updateSession = (next: Session | null) => {
    setSession(next)
    if (next) localStorage.setItem(SESSION_KEY, JSON.stringify(next))
    else localStorage.removeItem(SESSION_KEY)
  }

  if (!session) return <Login onAuthenticated={updateSession} />
  const operations = session.user.roles.some(role => ['CredentialingSpecialist', 'Manager', 'Administrator'].includes(role))
  return operations
    ? <OperationsApp session={session} onLogout={() => updateSession(null)} />
    : <ProviderPortal session={session} onLogout={() => updateSession(null)} />
}

function Login({ onAuthenticated }: { onAuthenticated: (session: Session) => void }) {
  const [email, setEmail] = useState('specialist@careops.local')
  const [password, setPassword] = useState(DEMO_PASSWORD)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    setLoading(true)
    try {
      const result = await api<Session>('/api/auth/login', undefined, { method: 'POST', body: JSON.stringify({ email, password }) })
      onAuthenticated(result)
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Unable to sign in.')
    } finally { setLoading(false) }
  }

  const personas = [
    ['Credentialing', 'specialist@careops.local', 'Manage reviews and queues'],
    ['Manager', 'manager@careops.local', 'Approve and monitor SLAs'],
    ['Provider', 'maya.chen@careops.local', 'Maintain credentials and schedule'],
    ['Administrator', 'admin@careops.local', 'Full workflow access'],
  ]

  return (
    <main className="login-shell">
      <section className="login-story">
        <div className="brand brand--light"><span className="brand-mark"><HeartPulse size={21} /></span><span>CareOps</span></div>
        <div className="story-copy">
          <span className="eyebrow eyebrow--light">Healthcare workforce operations</span>
          <h1>Every provider ready.<br />Every shift covered.</h1>
          <p>Credentialing, compliance, and scheduling signals in one operational workspace.</p>
          <div className="story-proof">
            <div><ShieldCheck /><span><strong>Policy-driven</strong><small>Role and workflow controls</small></span></div>
            <div><Activity /><span><strong>Real-time</strong><small>Live queue and SLA updates</small></span></div>
            <div><ClipboardCheck /><span><strong>Audit-ready</strong><small>Immutable decision history</small></span></div>
          </div>
        </div>
        <p className="story-foot">Built for high-trust clinical operations</p>
      </section>
      <section className="login-panel">
        <form className="login-card" onSubmit={submit}>
          <span className="eyebrow">Demo workspace</span>
          <h2>Welcome back</h2>
          <p className="muted">Sign in to your CareOps workspace.</p>
          <label>Email address<input value={email} onChange={event => setEmail(event.target.value)} type="email" autoComplete="username" /></label>
          <label>Password<input value={password} onChange={event => setPassword(event.target.value)} type="password" autoComplete="current-password" /></label>
          {error && <div className="form-error" role="alert"><AlertTriangle size={16} />{error}</div>}
          <button className="button button--primary button--full" disabled={loading}>{loading ? 'Signing in…' : 'Sign in securely'}</button>
          <div className="demo-divider"><span>or choose a demo role</span></div>
          <div className="persona-grid">
            {personas.map(([name, value, description]) => (
              <button type="button" className={email === value ? 'persona persona--active' : 'persona'} key={value} onClick={() => { setEmail(value); setPassword(DEMO_PASSWORD) }}>
                <span>{name}</span><small>{description}</small>
              </button>
            ))}
          </div>
          <p className="demo-note">All seeded accounts use <code>{DEMO_PASSWORD}</code>. Demo data only.</p>
        </form>
      </section>
    </main>
  )
}

function OperationsApp({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [queue, setQueue] = useState<PageResult<ProviderSummary> | null>(null)
  const [shifts, setShifts] = useState<Shift[]>([])
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<WorkflowStatus | ''>('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [view, setView] = useState<'dashboard' | 'schedule'>('dashboard')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [live, setLive] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const refresh = useCallback(async () => {
    setError('')
    try {
      const [dashboardResult, queueResult, shiftResult] = await Promise.all([
        api<Dashboard>('/api/dashboard', session),
        api<PageResult<ProviderSummary>>(`/api/providers?search=${encodeURIComponent(search)}${status ? `&status=${status}` : ''}&page=1&pageSize=25`, session),
        api<Shift[]>('/api/schedule/shifts', session),
      ])
      setDashboard(dashboardResult)
      setQueue(queueResult)
      setShifts(shiftResult)
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Unable to load the workspace.')
    } finally { setLoading(false) }
  }, [search, session, status])

  useEffect(() => {
    const initialLoad = window.setTimeout(() => void refresh(), 0)
    return () => window.clearTimeout(initialLoad)
  }, [refresh])
  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/workflow', { accessTokenFactory: () => session.accessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connection.on('workflowChanged', () => void refresh())
    connection.on('shiftChanged', () => void refresh())
    connection.on('notificationRaised', () => void refresh())
    connection.onreconnecting(() => setLive(false))
    connection.onreconnected(() => setLive(true))
    void connection.start().then(() => setLive(true)).catch(() => setLive(false))
    return () => { void connection.stop() }
  }, [refresh, session.accessToken])

  const roleLabel = session.user.roles[0]?.replace(/([a-z])([A-Z])/g, '$1 $2') ?? 'Operations'
  return (
    <div className="app-shell">
      <Sidebar view={view} setView={setView} open={sidebarOpen} close={() => setSidebarOpen(false)} />
      <div className="app-main">
        <header className="topbar">
          <button className="icon-button mobile-menu" onClick={() => setSidebarOpen(true)} aria-label="Open navigation"><Menu /></button>
          <div><span className="topbar-kicker">Operations workspace</span><h1>{view === 'dashboard' ? 'Credentialing command center' : 'Coverage schedule'}</h1></div>
          <div className="topbar-actions">
            <span className={live ? 'live-pill' : 'live-pill live-pill--offline'}><i />{live ? 'Live' : 'Reconnecting'}</span>
            <button className="icon-button" aria-label="Notifications"><Bell size={19} /><b>3</b></button>
            <div className="profile-chip"><span>{initials(session.user.email)}</span><div><strong>{session.user.email.split('@')[0]}</strong><small>{roleLabel}</small></div></div>
            <button className="icon-button" onClick={onLogout} aria-label="Sign out"><LogOut size={19} /></button>
          </div>
        </header>
        <main className="workspace">
          {error && <div className="page-error"><AlertTriangle size={17} />{error}<button onClick={() => void refresh()}>Retry</button></div>}
          {view === 'dashboard'
            ? <DashboardView dashboard={dashboard} queue={queue} loading={loading} search={search} setSearch={setSearch} status={status} setStatus={setStatus} onSelect={setSelectedId} refresh={refresh} />
            : <ScheduleView shifts={shifts} />}
        </main>
      </div>
      {selectedId && <ProviderDrawer providerId={selectedId} session={session} close={() => setSelectedId(null)} onChanged={refresh} />}
    </div>
  )
}

function Sidebar({ view, setView, open, close }: { view: string; setView: (view: 'dashboard' | 'schedule') => void; open: boolean; close: () => void }) {
  const nav = [
    { id: 'dashboard' as const, label: 'Command center', icon: LayoutDashboard },
    { id: 'schedule' as const, label: 'Coverage schedule', icon: CalendarDays },
  ]
  return (
    <aside className={open ? 'sidebar sidebar--open' : 'sidebar'}>
      <div className="brand"><span className="brand-mark"><HeartPulse size={21} /></span><span>CareOps</span><button onClick={close} className="sidebar-close"><X /></button></div>
      <nav>
        <span className="nav-label">Workspace</span>
        {nav.map(item => <button key={item.id} className={view === item.id ? 'nav-item nav-item--active' : 'nav-item'} onClick={() => { setView(item.id); close() }}><item.icon size={19} />{item.label}</button>)}
        <button className="nav-item"><Users size={19} />Provider directory</button>
        <button className="nav-item"><FileCheck2 size={19} />Compliance</button>
        <span className="nav-label">Manage</span>
        <button className="nav-item"><ShieldCheck size={19} />Policies & roles</button>
      </nav>
      <div className="sidebar-card"><span>System status</span><strong><i /> All systems operational</strong><small>Last checked just now</small></div>
      <div className="sidebar-version">CareOps <span>v1.0 MVP</span></div>
    </aside>
  )
}

function DashboardView({ dashboard, queue, loading, search, setSearch, status, setStatus, onSelect, refresh }: {
  dashboard: Dashboard | null
  queue: PageResult<ProviderSummary> | null
  loading: boolean
  search: string
  setSearch: (value: string) => void
  status: WorkflowStatus | ''
  setStatus: (value: WorkflowStatus | '') => void
  onSelect: (id: string) => void
  refresh: () => Promise<void>
}) {
  const metrics = [
    { label: 'Active providers', value: dashboard?.totalProviders ?? '—', note: `${dashboard?.complianceRate ?? 0}% compliant`, icon: Users, tone: 'teal' },
    { label: 'Open reviews', value: dashboard?.activeReviews ?? '—', note: 'Across all queues', icon: ClipboardCheck, tone: 'blue' },
    { label: 'SLA attention', value: dashboard?.slaAtRisk ?? '—', note: 'Due within 12 hours', icon: Clock3, tone: 'amber' },
    { label: 'Expiring soon', value: dashboard?.expiringWithin30Days ?? '—', note: 'Within 30 days', icon: AlertTriangle, tone: 'rose' },
  ]
  const filters: { label: string; value: WorkflowStatus | '' }[] = [
    { label: 'All queue', value: '' }, { label: 'Submitted', value: 'Submitted' },
    { label: 'Under review', value: 'UnderReview' }, { label: 'Needs info', value: 'NeedsInformation' },
  ]

  return (
    <>
      <section className="metric-grid">
        {metrics.map(metric => <article className="metric-card" key={metric.label}><div className={`metric-icon metric-icon--${metric.tone}`}><metric.icon size={20} /></div><span>{metric.label}</span><strong>{metric.value}</strong><small>{metric.note}</small></article>)}
      </section>
      <section className="dashboard-grid">
        <div className="panel queue-panel">
          <div className="panel-head">
            <div><span className="section-kicker">Today’s work</span><h2>Credentialing queue</h2></div>
            <button className="button button--secondary" onClick={() => void refresh()}><RefreshCw size={15} />Refresh</button>
          </div>
          <div className="queue-tools">
            <div className="filter-tabs">{filters.map(filter => <button key={filter.label} onClick={() => setStatus(filter.value)} className={status === filter.value ? 'active' : ''}>{filter.label}{filter.value && <span>{dashboard?.byStatus[filter.value] ?? 0}</span>}</button>)}</div>
            <label className="search-box"><Search size={17} /><input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search provider or NPI" aria-label="Search provider or NPI" /></label>
          </div>
          <div className="table-wrap">
            <table>
              <thead><tr><th>Provider</th><th>Status</th><th>Progress</th><th>SLA</th><th>Region</th><th><span className="sr-only">Open</span></th></tr></thead>
              <tbody>
                {loading && Array.from({ length: 5 }).map((_, index) => <tr className="skeleton-row" key={index}><td colSpan={6}><i /></td></tr>)}
                {!loading && queue?.items.map(provider => <ProviderRow provider={provider} key={provider.id} select={() => onSelect(provider.id)} />)}
                {!loading && !queue?.items.length && <tr><td colSpan={6}><div className="empty"><Search />No providers match this queue filter.</div></td></tr>}
              </tbody>
            </table>
          </div>
          <div className="panel-foot"><span>Showing {queue?.items.length ?? 0} of {queue?.total ?? 0} providers</span><span>Sorted by SLA priority</span></div>
        </div>
        <aside className="right-rail">
          <div className="panel attention-panel">
            <div className="panel-head panel-head--compact"><div><span className="section-kicker">Risk signals</span><h2>Needs attention</h2></div><span className="count-badge">{dashboard?.alerts.length ?? 0}</span></div>
            <div className="alert-list">
              {dashboard?.alerts.map(alert => <button key={alert.providerId} onClick={() => onSelect(alert.providerId)} className="alert-item"><span className={`alert-dot alert-dot--${alert.severity}`}><AlertTriangle size={15} /></span><span><strong>{alert.providerName}</strong><small>{alert.message}</small><em>{alert.dueAt ? relativeTime(alert.dueAt) : 'Review now'}</em></span><ChevronRight size={16} /></button>)}
              {!dashboard?.alerts.length && <div className="empty empty--small"><Check />No immediate risk signals.</div>}
            </div>
          </div>
          <div className="panel compliance-card">
            <div><span className="section-kicker">Network health</span><h2>Compliance rate</h2></div>
            <div className="compliance-gauge" style={{ '--progress': `${dashboard?.complianceRate ?? 0}%` } as React.CSSProperties}><strong>{dashboard?.complianceRate ?? 0}<small>%</small></strong></div>
            <p>Providers with an approved, current credential set.</p>
            <div className="legend"><span><i className="green" />Approved <b>{dashboard?.byStatus.Approved ?? 0}</b></span><span><i className="gray" />Other states <b>{(dashboard?.totalProviders ?? 0) - (dashboard?.byStatus.Approved ?? 0)}</b></span></div>
          </div>
        </aside>
      </section>
    </>
  )
}

function ProviderRow({ provider, select }: { provider: ProviderSummary; select: () => void }) {
  const completed = provider.checklistTotal ? Math.round(provider.checklistCompleted / provider.checklistTotal * 100) : 0
  const sla = slaLabel(provider.slaDueAt)
  return (
    <tr className="provider-row" onClick={select} tabIndex={0} onKeyDown={event => { if (event.key === 'Enter') select() }}>
      <td><div className="provider-cell"><span className="avatar">{initials(provider.displayName)}</span><span><strong>{provider.displayName}</strong><small>{provider.specialty} · NPI {provider.npi}</small></span></div></td>
      <td><StatusBadge status={provider.status} /></td>
      <td><div className="progress-cell"><span><i style={{ width: `${completed}%` }} /></span><small>{provider.checklistCompleted}/{provider.checklistTotal}</small></div></td>
      <td><span className={`sla sla--${sla.tone}`}><Clock3 size={14} />{sla.text}</span></td>
      <td><span className="region">{provider.region}</span></td>
      <td><button className="row-open" aria-label={`Open ${provider.displayName}`}><ChevronRight size={18} /></button></td>
    </tr>
  )
}

function ProviderDrawer({ providerId, session, close, onChanged }: { providerId: string; session: Session; close: () => void; onChanged: () => Promise<void> }) {
  const [provider, setProvider] = useState<ProviderDetail | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [comment, setComment] = useState('')
  const leadership = session.user.roles.some(role => ['Manager', 'Administrator'].includes(role))

  const load = useCallback(async () => {
    try { setProvider(await api<ProviderDetail>(`/api/providers/${providerId}`, session)) }
    catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to load provider.') }
  }, [providerId, session])
  useEffect(() => {
    const initialLoad = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(initialLoad)
  }, [load])

  const mutate = async (path: string, body?: object, method = 'POST') => {
    setBusy(true); setError('')
    try {
      await api<void>(path, session, { method, body: body ? JSON.stringify(body) : undefined })
      await Promise.all([load(), onChanged()])
    } catch (exception) { setError(exception instanceof Error ? exception.message : 'Update failed.') }
    finally { setBusy(false) }
  }

  const actions = useMemo(() => {
    if (!provider) return []
    const common: { label: string; status: WorkflowStatus; reason?: string; primary?: boolean }[] = []
    if (provider.status === 'Submitted') common.push({ label: 'Start review', status: 'UnderReview', primary: true }, { label: 'Request information', status: 'NeedsInformation', reason: 'Additional documentation is required.' })
    if (provider.status === 'UnderReview') common.push({ label: 'Request information', status: 'NeedsInformation', reason: 'Additional documentation is required.' })
    if (provider.status === 'Expired') common.push({ label: 'Reopen review', status: 'UnderReview', primary: true })
    if (leadership && provider.status === 'UnderReview') common.push({ label: 'Approve provider', status: 'Approved', primary: true })
    if (leadership && provider.status === 'Approved') common.push({ label: 'Suspend', status: 'Suspended', reason: 'Administrative compliance hold.' })
    if (leadership && provider.status === 'Suspended') common.push({ label: 'Return to review', status: 'UnderReview', primary: true })
    return common
  }, [leadership, provider])

  return (
    <div className="drawer-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) close() }}>
      <aside className="drawer" aria-label="Provider review detail">
        <div className="drawer-head"><div><span className="section-kicker">Provider review</span><h2>{provider?.displayName ?? 'Loading…'}</h2>{provider && <small>NPI {provider.npi} · {provider.specialty}</small>}</div><button className="icon-button" onClick={close} aria-label="Close"><X /></button></div>
        {error && <div className="form-error"><AlertTriangle size={16} />{error}</div>}
        {!provider ? <div className="drawer-loading"><RefreshCw /> Loading credential record…</div> : <>
          <div className="drawer-status"><StatusBadge status={provider.status} /><span>{slaLabel(provider.slaDueAt).text}</span></div>
          {actions.length > 0 && <div className="action-strip">{actions.map(action => <button disabled={busy} key={action.status} className={action.primary ? 'button button--primary' : 'button button--secondary'} onClick={() => void mutate(`/api/providers/${provider.id}/transition`, { status: action.status, reason: action.reason })}>{action.label}</button>)}</div>}
          <DrawerSection title="Credentials" icon={<FileCheck2 size={17} />} count={provider.credentials.length}>
            {provider.credentials.map(credential => <div className="record-row" key={credential.id}><span className="record-icon"><FileCheck2 size={17} /></span><span><strong>{credential.type}</strong><small>{credential.originalFileName} · expires {formatDate(credential.expiresOn)}</small></span><StatusBadge status={credential.status} />{credential.status === 'Pending' && <button disabled={busy} className="text-button" onClick={() => void mutate(`/api/providers/${provider.id}/credentials/${credential.id}/review`, { status: 'Verified' })}>Verify</button>}</div>)}
          </DrawerSection>
          <DrawerSection title="Verification checklist" icon={<ClipboardCheck size={17} />} count={provider.checklist.filter(item => item.result === 'Passed').length}>
            {provider.checklist.map(item => <div className="check-row" key={item.id}><span className={item.result === 'Passed' ? 'check-box checked' : 'check-box'}><Check size={14} /></span><span><strong>{item.name}</strong><small>{item.evidence ?? (item.isRequired ? 'Required check' : 'Optional check')}</small></span>{item.result !== 'Passed' && <button disabled={busy} className="text-button" onClick={() => void mutate(`/api/providers/${provider.id}/checklist/${item.id}`, { result: 'Passed', evidence: 'Primary source verified.' }, 'PUT')}>Pass</button>}</div>)}
          </DrawerSection>
          <DrawerSection title="Review conversation" icon={<MessageSquare size={17} />} count={provider.comments.length}>
            <form className="comment-form" onSubmit={event => { event.preventDefault(); if (!comment.trim()) return; void mutate(`/api/providers/${provider.id}/comments`, { body: comment, visibleToProvider: true }).then(() => setComment('')) }}><textarea value={comment} onChange={event => setComment(event.target.value)} placeholder="Add a provider-visible note…" /><button className="button button--primary" disabled={busy || !comment.trim()}>Add note</button></form>
            {provider.comments.map(item => <div className="comment" key={item.id}><span>{initials(item.authorUserId)}</span><div><strong>{item.visibleToProvider ? 'Shared with provider' : 'Internal note'}</strong><p>{item.body}</p><small>{relativeTime(item.createdAt)}</small></div></div>)}
          </DrawerSection>
          <DrawerSection title="Audit history" icon={<Activity size={17} />} count={provider.auditHistory.length}>
            {provider.auditHistory.slice(0, 8).map(item => <div className="timeline-item" key={item.id}><i /><span><strong>{item.action.replace('.', ' ')}</strong><small>{item.details}</small><em>{formatDateTime(item.createdAt)}</em></span></div>)}
          </DrawerSection>
        </>}
      </aside>
    </div>
  )
}

function DrawerSection({ title, icon, count, children }: { title: string; icon: ReactNode; count: number; children: ReactNode }) {
  return <section className="drawer-section"><h3>{icon}{title}<span>{count}</span></h3><div>{children}</div></section>
}

function ScheduleView({ shifts }: { shifts: Shift[] }) {
  return <section className="panel schedule-panel"><div className="panel-head"><div><span className="section-kicker">Upcoming coverage</span><h2>Provider shift schedule</h2></div><button className="button button--primary">Create coverage shift</button></div><div className="schedule-list">{shifts.map(shift => <article key={shift.id}><div className="date-tile"><strong>{new Date(shift.startsAt).getDate()}</strong><small>{new Date(shift.startsAt).toLocaleString('en-US', { month: 'short' })}</small></div><div><strong>{shift.department}</strong><span>{shift.facility}</span><small>{formatTime(shift.startsAt)} – {formatTime(shift.endsAt)}</small></div><StatusBadge status={shift.status} /></article>)}{!shifts.length && <div className="empty"><CalendarDays />No upcoming shifts are scheduled.</div>}</div></section>
}

function ProviderPortal({ session, onLogout }: { session: Session; onLogout: () => void }) {
  const [provider, setProvider] = useState<ProviderDetail | null>(null)
  const [shifts, setShifts] = useState<Shift[]>([])
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    try {
      const [profile, schedule] = await Promise.all([api<ProviderDetail>('/api/providers/me', session), api<Shift[]>('/api/schedule/shifts', session)])
      setProvider(profile); setShifts(schedule)
    } catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to load your portal.') }
  }, [session])
  useEffect(() => {
    const initialLoad = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(initialLoad)
  }, [load])
  return <div className="provider-portal"><header><div className="brand"><span className="brand-mark"><HeartPulse /></span>CareOps</div><div><span>{session.user.email}</span><button className="icon-button" onClick={onLogout}><LogOut /></button></div></header><main><div className="portal-welcome"><span className="eyebrow">Provider workspace</span><h1>Good morning, {provider?.displayName.split(' ')[0] ?? 'provider'}.</h1><p>Keep your credentialing record current and stay ahead of upcoming coverage.</p></div>{error && <div className="page-error">{error}</div>}<section className="portal-grid"><article className="panel portal-status"><div><span className="section-kicker">Credentialing status</span><StatusBadge status={provider?.status ?? 'Draft'} /></div><h2>{provider?.status === 'Approved' ? 'You’re cleared for scheduling' : 'Your record is being reviewed'}</h2><p>{provider?.checklist.filter(item => item.result === 'Passed').length ?? 0} of {provider?.checklist.length ?? 0} verification checks complete.</p><div className="big-progress"><i style={{ width: `${provider?.checklist.length ? provider.checklist.filter(item => item.result === 'Passed').length / provider.checklist.length * 100 : 0}%` }} /></div></article><article className="panel portal-credentials"><div className="panel-head panel-head--compact"><h2>Credentials</h2><button className="button button--secondary">Add metadata</button></div>{provider?.credentials.map(credential => <div className="record-row" key={credential.id}><FileCheck2 /><span><strong>{credential.type}</strong><small>Expires {formatDate(credential.expiresOn)}</small></span><StatusBadge status={credential.status} /></div>)}</article><article className="panel portal-shifts"><div className="panel-head panel-head--compact"><h2>Upcoming shifts</h2></div>{shifts.map(shift => <div className="record-row" key={shift.id}><CalendarDays /><span><strong>{shift.department}</strong><small>{formatDateTime(shift.startsAt)} · {shift.facility}</small></span><StatusBadge status={shift.status} /></div>)}{!shifts.length && <div className="empty empty--small">No coverage shifts yet.</div>}</article></section></main></div>
}

function StatusBadge({ status }: { status: string }) {
  return <span className={`status status--${status.toLowerCase()}`}><i />{status.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>
}

function initials(value: string) {
  return value.includes('@') ? value.slice(0, 2).toUpperCase() : value.split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase()
}

function formatDate(value: string) { return new Date(`${value}T00:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) }
function formatDateTime(value: string) { return new Date(value).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }) }
function formatTime(value: string) { return new Date(value).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }) }
function relativeTime(value: string) {
  const hours = Math.round((new Date(value).getTime() - Date.now()) / 3_600_000)
  if (hours < -24) return `${Math.abs(Math.round(hours / 24))}d overdue`
  if (hours < 0) return `${Math.abs(hours)}h overdue`
  if (hours < 24) return `Due in ${hours}h`
  return `Due in ${Math.round(hours / 24)}d`
}
function slaLabel(value?: string) {
  if (!value) return { text: 'No active SLA', tone: 'neutral' }
  const hours = (new Date(value).getTime() - Date.now()) / 3_600_000
  if (hours < 0) return { text: `${Math.max(1, Math.round(Math.abs(hours)))}h overdue`, tone: 'critical' }
  if (hours <= 12) return { text: `${Math.max(1, Math.round(hours))}h remaining`, tone: 'warning' }
  return { text: `${Math.ceil(hours / 24)}d remaining`, tone: 'healthy' }
}

export default App
