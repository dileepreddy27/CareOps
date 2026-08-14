export type WorkflowStatus = 'Draft' | 'Submitted' | 'UnderReview' | 'NeedsInformation' | 'Approved' | 'Suspended' | 'Expired'

export interface User {
  id: string
  email: string
  roles: string[]
  providerProfileId?: string
}

export interface Session {
  accessToken: string
  expiresAt: string
  user: User
}

export interface Dashboard {
  totalProviders: number
  activeReviews: number
  slaAtRisk: number
  expiringWithin30Days: number
  complianceRate: number
  byStatus: Record<string, number>
  alerts: Alert[]
}

export interface Alert {
  providerId: string
  providerName: string
  severity: 'critical' | 'warning'
  message: string
  dueAt?: string
}

export interface ProviderSummary {
  id: string
  displayName: string
  npi: string
  specialty: string
  region: string
  status: WorkflowStatus
  assignedReviewerId?: string
  slaDueAt?: string
  credentialCount: number
  expiringCredentialCount: number
  checklistCompleted: number
  checklistTotal: number
  updatedAt: string
}

export interface PageResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export interface Credential {
  id: string
  type: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  issuedOn: string
  expiresOn: string
  status: 'Pending' | 'Verified' | 'Rejected' | 'Expired'
  verifiedAt?: string
}

export interface ChecklistItem {
  id: string
  name: string
  isRequired: boolean
  sortOrder: number
  result: 'Pending' | 'Passed' | 'Failed' | 'NotApplicable'
  evidence?: string
  completedAt?: string
}

export interface ProviderDetail extends Omit<ProviderSummary, 'credentialCount' | 'expiringCredentialCount' | 'checklistCompleted' | 'checklistTotal' | 'updatedAt'> {
  userId: string
  phone?: string
  submittedAt?: string
  credentials: Credential[]
  checklist: ChecklistItem[]
  comments: { id: string; authorUserId: string; body: string; visibleToProvider: boolean; createdAt: string }[]
  auditHistory: { id: string; action: string; details: string; createdAt: string }[]
}

export interface Shift {
  id: string
  providerProfileId?: string
  facility: string
  department: string
  startsAt: string
  endsAt: string
  status: 'Open' | 'Offered' | 'Confirmed' | 'Cancelled'
}
