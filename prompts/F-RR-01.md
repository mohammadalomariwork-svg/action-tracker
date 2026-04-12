# F-RR-01 — Risk Register Models, Service, Routing

## Context
The backend now exposes `api/projects/{projectId}/risks` endpoints. This prompt sets up the Angular frontend layer: TypeScript models, API service, and route configuration.

## Requirements

### 1. Create models in `src/app/models/project-risk.model.ts`

```typescript
export interface ProjectRisk {
  id: string;
  riskCode: string;
  projectId: string;
  projectName: string;
  title: string;
  description: string;
  category: string;
  probabilityScore: number;
  impactScore: number;
  riskScore: number;
  riskRating: string; // Critical, High, Medium, Low
  status: string; // Open, Mitigating, Accepted, Transferred, Closed
  mitigationPlan?: string;
  contingencyPlan?: string;
  riskOwnerUserId?: string;
  riskOwnerDisplayName?: string;
  identifiedDate: string;
  dueDate?: string;
  closedDate?: string;
  notes?: string;
  createdByUserId?: string;
  createdByDisplayName?: string;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectRiskSummary {
  id: string;
  riskCode: string;
  title: string;
  category: string;
  riskScore: number;
  riskRating: string;
  status: string;
  riskOwnerDisplayName?: string;
  identifiedDate: string;
  dueDate?: string;
}

export interface CreateProjectRisk {
  projectId: string;
  title: string;
  description: string;
  category: string;
  probabilityScore: number;
  impactScore: number;
  status?: string;
  mitigationPlan?: string;
  contingencyPlan?: string;
  riskOwnerUserId?: string;
  dueDate?: string;
  notes?: string;
}

export interface UpdateProjectRisk {
  title: string;
  description: string;
  category: string;
  probabilityScore: number;
  impactScore: number;
  status: string;
  mitigationPlan?: string;
  contingencyPlan?: string;
  riskOwnerUserId?: string;
  dueDate?: string;
  closedDate?: string;
  notes?: string;
}

export interface ProjectRiskStats {
  totalRisks: number;
  openRisks: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  closedCount: number;
  overdueCount: number;
}

export const RISK_CATEGORIES: string[] = [
  'Technical', 'Schedule', 'Resource', 'Budget', 'External', 'Quality', 'Scope', 'Compliance'
];

export const RISK_STATUSES: string[] = [
  'Open', 'Mitigating', 'Accepted', 'Transferred', 'Closed'
];
```

### 2. Create `ProjectRiskService` in `src/app/services/project-risk.service.ts`

Methods (all return `Observable`):
- `getRisksByProject(projectId: string, page: number, pageSize: number, status?: string, rating?: string, category?: string): Observable<PagedResult<ProjectRiskSummary>>`
- `getRiskById(projectId: string, riskId: string): Observable<ApiResponse<ProjectRisk>>`
- `getRiskStats(projectId: string): Observable<ApiResponse<ProjectRiskStats>>`
- `createRisk(projectId: string, dto: CreateProjectRisk): Observable<ApiResponse<ProjectRisk>>`
- `updateRisk(projectId: string, riskId: string, dto: UpdateProjectRisk): Observable<ApiResponse<ProjectRisk>>`
- `deleteRisk(projectId: string, riskId: string): Observable<ApiResponse<void>>`
- `restoreRisk(projectId: string, riskId: string): Observable<ApiResponse<void>>`

Base URL: `${environment.apiUrl}/projects/${projectId}/risks`

Use `HttpClient` with the existing `ApiResponse<T>` and `PagedResult<T>` wrappers.

### 3. Add route for risk detail
In the existing app routing configuration, add:
- `/projects/:projectId/risks/:riskId` → `RiskDetailComponent` (to be created in F-RR-03)

This is the ONLY modification to an existing file.

## Rules
- Strongly typed — no `any`
- Use existing `ApiResponse<T>` and `PagedResult<T>` interfaces
- Injectable service with `providedIn: 'root'`
