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
