import { ActionItem } from './action-item.model';

export interface DashboardKpi {
  totalActions: number;
  completionRate: number;
  onTimeDeliveryRate: number;
  activeEscalations: number;
  teamVelocity: number;
  criticalHighCount: number;
  inProgressCount: number;
  overdueCount: number;
}

export interface StatusBreakdown {
  status: string;
  count: number;
  percentage: number;
  color: string;
}

export interface TeamWorkload {
  userId: string;
  userName: string;
  assignedCount: number;
  completedCount: number;
  overdueCount: number;
  completionPercentage: number;
}

export interface AtRiskItem {
  id: string;
  actionId: string;
  title: string;
  assigneeName: string;
  priority: string;
  status: string;
  dueDate: string;
  daysOverdue: number;
  isEscalated: boolean;
  severityLevel: string;
}

export interface ManagementDashboard {
  kpis: DashboardKpi;
  statusBreakdown: StatusBreakdown[];
  teamWorkload: TeamWorkload[];
  atRiskItems: AtRiskItem[];
  recentActivity: any[];
  criticalActions: ActionItem[];
}
