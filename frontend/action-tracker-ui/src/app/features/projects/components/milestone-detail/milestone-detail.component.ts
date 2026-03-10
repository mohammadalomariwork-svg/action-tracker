import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { MilestoneService } from '../../services/milestone.service';
import {
  MilestoneResponse,
  MilestoneStatus,
  MilestoneStatusLabels,
} from '../../models/milestone.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { BreadcrumbComponent } from '../../../../shared/components/breadcrumb/breadcrumb.component';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';

@Component({
  selector: 'app-milestone-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, PageHeaderComponent, BreadcrumbComponent, CommentsSectionComponent, DocumentsSectionComponent],
  templateUrl: './milestone-detail.component.html',
  styleUrl: './milestone-detail.component.scss',
})
export class MilestoneDetailComponent implements OnInit {
  private readonly milestoneService = inject(MilestoneService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  projectId!: string;
  milestoneId!: string;
  milestone: MilestoneResponse | null = null;
  isLoading = false;
  errorMessage: string | null = null;

  readonly MilestoneStatus = MilestoneStatus;
  readonly MilestoneStatusLabels = MilestoneStatusLabels;

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('projectId')!;
    this.milestoneId = this.route.snapshot.paramMap.get('milestoneId')!;
    this.loadMilestone();
  }

  private loadMilestone(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.milestoneService.getById(this.projectId, this.milestoneId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.milestone = res.data ?? null;
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load milestone details.';
          this.isLoading = false;
        },
      });
  }

  statusClass(s: MilestoneStatus): string {
    switch (+s) {
      case MilestoneStatus.NotStarted: return 'badge bg-secondary';
      case MilestoneStatus.InProgress: return 'badge bg-primary';
      case MilestoneStatus.Completed:  return 'badge bg-success';
      case MilestoneStatus.Delayed:    return 'badge bg-warning text-dark';
      case MilestoneStatus.Cancelled:  return 'badge bg-danger';
      default:                         return 'badge bg-light text-dark';
    }
  }

  varianceClass(days: number | null | undefined): string {
    if (days == null) return '';
    if (days > 0) return 'text-danger';
    if (days < 0) return 'text-success';
    return '';
  }

  varianceLabel(days: number | null | undefined): string {
    if (days == null) return '';
    if (days === 0) return 'On schedule';
    if (days > 0) return `${days} day${days > 1 ? 's' : ''} behind`;
    const abs = Math.abs(days);
    return `${abs} day${abs > 1 ? 's' : ''} ahead`;
  }
}
