import { Injectable, inject, NgZone } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppNotification, NotificationSummary } from '../models/notification.model';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { AuthService } from './auth.service';
import { ToastService } from './toast.service';
import * as signalR from '@microsoft/signalr';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly zone = inject(NgZone);
  private readonly router = inject(Router);
  private readonly toastSvc = inject(ToastService);
  private readonly apiUrl = `${environment.apiUrl}/notifications`;

  /** Set by header component to avoid circular DI with WorkflowStateService */
  private workflowRefreshCallback: (() => void) | null = null;
  registerWorkflowRefresh(cb: () => void): void {
    this.workflowRefreshCallback = cb;
  }

  private hubConnection: signalR.HubConnection | null = null;
  private initialized = false;

  private readonly _unreadCount$ = new BehaviorSubject<number>(0);
  readonly unreadCount$ = this._unreadCount$.asObservable();

  private readonly _newNotification$ = new Subject<AppNotification>();
  readonly newNotification$ = this._newNotification$.asObservable();

  private readonly _latestNotifications$ = new BehaviorSubject<AppNotification[]>([]);
  readonly latestNotifications$ = this._latestNotifications$.asObservable();

  /** Call once after login to bootstrap state and start SignalR. Idempotent. */
  init(): void {
    if (this.initialized) return;
    this.initialized = true;
    this.loadSummary();
    this.loadLatestNotifications();
    this.startSignalR();
  }

  /** Tear down on logout. */
  destroy(): void {
    this.hubConnection?.stop();
    this.hubConnection = null;
    this.initialized = false;
    this._unreadCount$.next(0);
    this._latestNotifications$.next([]);
  }

  // ── REST API ──────────────────────────────────────────────

  getAll(page: number, pageSize: number, isRead?: boolean, type?: string): Observable<ApiResponse<PagedResult<AppNotification>>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (isRead !== undefined) params = params.set('isRead', isRead);
    if (type) params = params.set('type', type);
    return this.http.get<ApiResponse<PagedResult<AppNotification>>>(this.apiUrl, { params });
  }

  getSummary(): Observable<ApiResponse<NotificationSummary>> {
    return this.http.get<ApiResponse<NotificationSummary>>(`${this.apiUrl}/summary`);
  }

  getUnreadCount(): Observable<ApiResponse<number>> {
    return this.http.get<ApiResponse<number>>(`${this.apiUrl}/unread-count`);
  }

  markAsRead(id: string): Observable<ApiResponse<string>> {
    return this.http.patch<ApiResponse<string>>(`${this.apiUrl}/${id}/read`, {});
  }

  markAllAsRead(): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(`${this.apiUrl}/mark-all-read`, {});
  }

  delete(id: string): Observable<ApiResponse<string>> {
    return this.http.delete<ApiResponse<string>>(`${this.apiUrl}/${id}`);
  }

  deleteAllRead(): Observable<ApiResponse<string>> {
    return this.http.delete<ApiResponse<string>>(`${this.apiUrl}/read`);
  }

  // ── Internal helpers ──────────────────────────────────────

  private loadSummary(): void {
    this.getSummary().subscribe({
      next: res => {
        if (res.success) {
          this._unreadCount$.next(res.data.unreadCount);
        }
      },
    });
  }

  private loadLatestNotifications(): void {
    this.getAll(1, 10).subscribe({
      next: res => {
        if (res.success) {
          this._latestNotifications$.next(res.data.items);
        }
      },
    });
  }

  refreshUnreadCount(): void {
    this.getUnreadCount().subscribe({
      next: res => {
        if (res.success) this._unreadCount$.next(res.data);
      },
    });
  }

  decrementUnread(): void {
    const current = this._unreadCount$.value;
    if (current > 0) this._unreadCount$.next(current - 1);
  }

  clearUnread(): void {
    this._unreadCount$.next(0);
  }

  // ── Workflow toast handling ────────────────────────────────

  private readonly WORKFLOW_ACTION_TYPES = new Set([
    'DateChangeRequested', 'StatusChangeRequested',
    'DateChangeApproved', 'StatusChangeApproved',
    'DateChangeRejected', 'StatusChangeRejected',
    'ActionItemEscalated', 'EscalationDirectionGiven',
  ]);

  private handleWorkflowToast(notification: AppNotification): void {
    const action = notification.actionType;
    if (!this.WORKFLOW_ACTION_TYPES.has(action)) return;

    const msg = notification.message || notification.title;

    switch (action) {
      case 'DateChangeRequested':
      case 'StatusChangeRequested':
        this.toastSvc.info(msg);
        break;
      case 'DateChangeApproved':
      case 'StatusChangeApproved':
        this.toastSvc.success(msg);
        break;
      case 'DateChangeRejected':
      case 'StatusChangeRejected':
        this.toastSvc.warning(msg);
        break;
      case 'ActionItemEscalated':
        this.toastSvc.error(msg);
        break;
      case 'EscalationDirectionGiven':
        this.toastSvc.info(msg);
        break;
    }

    // Refresh workflow pending count
    if (this.workflowRefreshCallback) {
      this.workflowRefreshCallback();
    }
  }

  // ── SignalR ───────────────────────────────────────────────

  private startSignalR(): void {
    const hubUrl = environment.apiUrl.replace('/api', '') + '/hubs/notifications';

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.authService.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveNotification', (notification: AppNotification) => {
      this.zone.run(() => {
        this._newNotification$.next(notification);
        this._unreadCount$.next(this._unreadCount$.value + 1);
        const current = this._latestNotifications$.value;
        this._latestNotifications$.next([notification, ...current].slice(0, 10));

        // Workflow-specific toast handling
        this.handleWorkflowToast(notification);
      });
    });

    this.hubConnection.on('UnreadCountUpdated', (count: number) => {
      this.zone.run(() => {
        this._unreadCount$.next(count);
      });
    });

    this.hubConnection.start().catch(err => {
      console.warn('SignalR connection failed, will retry:', err);
    });
  }
}
