import {
  Component,
  OnInit,
  OnDestroy,
  HostListener,
  ElementRef,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { formatDistanceToNow } from 'date-fns';
import { NotificationService } from '../../../core/services/notification.service';
import { AppNotification } from '../../../core/models/notification.model';

const TYPE_ICONS: Record<string, string> = {
  ActionItem:          'bi-check2-square',
  Project:             'bi-folder',
  Milestone:           'bi-flag',
  Workspace:           'bi-building',
  Risk:                'bi-exclamation-triangle',
  Kpi:                 'bi-graph-up',
  StrategicObjective:  'bi-bullseye',
  System:              'bi-info-circle',
};

const ACTION_COLORS: Record<string, string> = {
  Created:       '#0d6efd',
  Completed:     '#198754',
  Overdue:       '#dc3545',
  Escalated:     '#fd7e14',
  StatusChanged: '#6f42c1',
  Assigned:      '#20c997',
  Updated:       '#0d6efd',
  Deleted:       '#6c757d',
};

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss',
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly elRef = inject(ElementRef);

  open = signal(false);
  shaking = signal(false);
  unreadCount = signal(0);
  notifications = signal<AppNotification[]>([]);

  private subs: Subscription[] = [];

  ngOnInit(): void {
    this.notificationService.init();

    this.subs.push(
      this.notificationService.unreadCount$.subscribe(count => {
        this.unreadCount.set(count);
      }),
    );

    this.subs.push(
      this.notificationService.latestNotifications$.subscribe(items => {
        this.notifications.set(items);
      }),
    );

    this.subs.push(
      this.notificationService.newNotification$.subscribe(() => {
        this.shaking.set(true);
        setTimeout(() => this.shaking.set(false), 600);
      }),
    );

    // Close dropdown on route change
    this.subs.push(
      this.router.events
        .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
        .subscribe(() => this.open.set(false)),
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  toggle(): void {
    this.open.update(v => !v);
  }

  @HostListener('document:click', ['$event.target'])
  onDocumentClick(target: EventTarget | null): void {
    if (!(target instanceof Node)) return;
    if (!this.elRef.nativeElement.contains(target)) {
      this.open.set(false);
    }
  }

  markAllRead(): void {
    this.notificationService.markAllAsRead().subscribe({
      next: () => {
        this.notificationService.clearUnread();
        this.notifications.update(items =>
          items.map(n => ({ ...n, isRead: true })),
        );
      },
    });
  }

  clickNotification(n: AppNotification): void {
    if (!n.isRead) {
      this.notificationService.markAsRead(n.id).subscribe();
      this.notificationService.decrementUnread();
      n.isRead = true;
    }
    this.open.set(false);
    if (n.url) {
      this.router.navigateByUrl(n.url);
    }
  }

  viewAll(): void {
    this.open.set(false);
    this.router.navigate(['/notifications']);
  }

  getIcon(type: string): string {
    return TYPE_ICONS[type] ?? 'bi-bell';
  }

  getIconColor(actionType: string): string {
    return ACTION_COLORS[actionType] ?? '#6c757d';
  }

  getBadgeText(): string {
    const count = this.unreadCount();
    if (count <= 0) return '';
    return count > 9 ? '9+' : count.toString();
  }

  truncate(text: string, max: number): string {
    return text.length > max ? text.substring(0, max) + '...' : text;
  }

  relativeTime(dateStr: string): string {
    try {
      return formatDistanceToNow(new Date(dateStr), { addSuffix: true });
    } catch {
      return '';
    }
  }
}
