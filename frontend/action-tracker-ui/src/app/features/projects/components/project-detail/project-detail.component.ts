import { Component, OnInit, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectService } from '../../services/project.service';
import {
  ProjectResponse,
  ProjectType,
  ProjectStatus,
  ProjectPriority,
} from '../../models/project.models';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { CommentsSectionComponent } from '../../../../shared/components/comments-section/comments-section.component';
import { DocumentsSectionComponent } from '../../../../shared/components/documents-section/documents-section.component';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, PageHeaderComponent, CommentsSectionComponent, DocumentsSectionComponent],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  projectId!: string;
  project: ProjectResponse | null = null;
  isLoading = false;
  errorMessage: string | null = null;

  readonly ProjectType = ProjectType;
  readonly ProjectStatus = ProjectStatus;
  readonly ProjectPriority = ProjectPriority;

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id')!;
    this.loadProject();
  }

  private loadProject(): void {
    this.isLoading = true;
    this.errorMessage = null;

    this.projectService.getById(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.project = res.data ?? null;
          this.isLoading = false;
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Failed to load project details.';
          this.isLoading = false;
        },
      });
  }

  priorityClass(p: ProjectPriority): string {
    switch (+p) {
      case ProjectPriority.Critical: return 'badge bg-danger';
      case ProjectPriority.High:     return 'badge bg-warning text-dark';
      case ProjectPriority.Medium:   return 'badge bg-info text-dark';
      case ProjectPriority.Low:      return 'badge bg-secondary';
      default:                       return 'badge bg-light text-dark';
    }
  }

  statusClass(s: ProjectStatus): string {
    switch (+s) {
      case ProjectStatus.Draft:     return 'badge bg-secondary';
      case ProjectStatus.Active:    return 'badge bg-primary';
      case ProjectStatus.OnHold:    return 'badge bg-warning text-dark';
      case ProjectStatus.Completed: return 'badge bg-success';
      case ProjectStatus.Cancelled: return 'badge bg-danger';
      default:                      return 'badge bg-light text-dark';
    }
  }

  typeClass(t: ProjectType): string {
    switch (+t) {
      case ProjectType.Strategic:   return 'badge bg-primary';
      case ProjectType.Operational: return 'badge bg-info text-dark';
      default:                      return 'badge bg-light text-dark';
    }
  }
}
