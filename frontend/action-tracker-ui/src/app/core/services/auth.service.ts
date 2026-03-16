import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  AuthUser,
  LoginRequest,
  RefreshTokenRequest,
} from '../models/auth.models';
import { ApiResponse } from '../models/api-response.model';

// ─── localStorage key constants ─────────────────────────────────────────────
const ACCESS_TOKEN_KEY  = 'access_token';
const REFRESH_TOKEN_KEY = 'refresh_token';
const AUTH_USER_KEY     = 'auth_user';

/**
 * Root-level authentication service.
 *
 * Manages the full authentication lifecycle for both local (email/password)
 * and Azure AD federated login flows. Persists the session to `localStorage`
 * and keeps a reactive `BehaviorSubject` in sync so any part of the
 * application can subscribe to the current user.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http   = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly baseUrl = `${environment.apiUrl}/auth`;

  /** @internal Mutable source — never exposed directly. */
  private readonly _currentUser$ = new BehaviorSubject<AuthUser | null>(null);

  /**
   * Observable stream of the currently authenticated user.
   * Emits `null` when no session is active.
   */
  get currentUser$(): Observable<AuthUser | null> {
    return this._currentUser$.asObservable();
  }

  constructor() {
    this.restoreSession();
  }

  // ─── Public API ─────────────────────────────────────────────────────────────

  /**
   * Authenticates a locally registered user using their email and password.
   *
   * On success, stores `access_token`, `refresh_token`, and `auth_user` in
   * `localStorage` and emits the resolved `AuthUser` to `currentUser$`.
   *
   * @param request - The login credentials (`email` + `password`).
   * @returns An `Observable` that emits the raw `AuthResponse` from the API.
   */
  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.baseUrl}/login`, request)
      .pipe(
        map(res => res.data),
        tap(response => this.persistSession(response)),
      );
  }

  /**
   * Authenticates a user using an Azure AD access token obtained from MSAL.
   *
   * Sends the token to the API for server-side validation. On success applies
   * the same storage and state-emission behaviour as `login()`.
   *
   * @param accessToken - The MSAL access token acquired after the Microsoft
   *                      Entra ID interactive login on the frontend.
   * @returns An `Observable` that emits the raw `AuthResponse` from the API.
   */
  loginWithAzureAd(accessToken: string): Observable<AuthResponse> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.baseUrl}/azure-login`, { accessToken })
      .pipe(
        map(res => res.data),
        tap(response => this.persistSession(response)),
      );
  }

  /**
   * Exchanges the currently stored token pair for a fresh one without
   * requiring the user to log in again.
   *
   * Reads `access_token` and `refresh_token` from `localStorage`, posts
   * them to the refresh endpoint, and persists the new tokens on success.
   *
   * @returns An `Observable` that emits the new `AuthResponse`.
   */
  refreshToken(): Observable<AuthResponse> {
    const request: RefreshTokenRequest = {
      accessToken:  localStorage.getItem(ACCESS_TOKEN_KEY)  ?? '',
      refreshToken: localStorage.getItem(REFRESH_TOKEN_KEY) ?? '',
    };

    return this.http
      .post<ApiResponse<AuthResponse>>(`${this.baseUrl}/refresh-token`, request)
      .pipe(
        map(res => res.data),
        tap(response => this.persistSession(response)),
      );
  }

  /**
   * Logs out the current user.
   *
   * Posts to `/auth/logout` to revoke all server-side refresh tokens, then
   * — regardless of whether the HTTP call succeeds — clears the three
   * `localStorage` keys (`access_token`, `refresh_token`, `auth_user`),
   * emits `null` to `currentUser$`, and navigates to `/login`.
   */
  logout(): void {
    this.http.post(`${this.baseUrl}/logout`, {}).subscribe({
      complete: () => this.clearAndRedirect(),
      error:    () => this.clearAndRedirect(), // always clear locally
    });
  }

  /**
   * Returns `true` when an unexpired access token is present in `localStorage`.
   *
   * Expiry is checked by base64-decoding the JWT payload and comparing the
   * `exp` claim (Unix seconds) against `Date.now()` (milliseconds).
   *
   * @returns `true` if authenticated and the token has not expired.
   */
  isAuthenticated(): boolean {
    const token = localStorage.getItem(ACCESS_TOKEN_KEY);
    if (!token) return false;

    try {
      const payload = JSON.parse(atob(token.split('.')[1])) as { exp: number };
      return Date.now() < payload.exp * 1000;
    } catch {
      return false;
    }
  }

  /**
   * Returns `true` if the current user has been assigned the given role.
   *
   * Reads synchronously from the `BehaviorSubject` value — no subscription
   * required.
   *
   * @param role - The role name to check (e.g. `'Admin'`, `'Manager'`).
   * @returns `true` when the current user's `roles` array contains `role`.
   */
  hasRole(role: string): boolean {
    return this._currentUser$.getValue()?.roles.includes(role) ?? false;
  }

  /**
   * Returns the raw access token string stored in `localStorage`, or `null`
   * if no token is present (i.e. the user is not logged in).
   *
   * Intended for use by HTTP interceptors that need to attach the token to
   * outgoing requests.
   *
   * @returns The JWT access token, or `null`.
   */
  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  // ─── Private helpers ─────────────────────────────────────────────────────

  /**
   * Called once in the constructor to rehydrate `currentUser$` from any
   * session that was persisted in `localStorage` before the page was refreshed.
   * Clears stale state if the stored token is already expired.
   */
  private restoreSession(): void {
    const raw = localStorage.getItem(AUTH_USER_KEY);
    if (!raw) return;

    try {
      const user = JSON.parse(raw) as AuthUser;
      if (this.isAuthenticated()) {
        this._currentUser$.next(user);
      } else {
        this.clearStorage();
      }
    } catch {
      this.clearStorage();
    }
  }

  /**
   * Writes a successful auth response to `localStorage` and synchronises
   * `currentUser$`. Shared by all login flows and `refreshToken()`.
   */
  private persistSession(response: AuthResponse): void {
    const user: AuthUser = {
      userId:        response.userId,
      email:         response.email,
      displayName:   response.displayName,
      loginProvider: response.loginProvider,
      roles:         response.roles,
      isAuthenticated: true,
    };

    localStorage.setItem(ACCESS_TOKEN_KEY,  response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(AUTH_USER_KEY,     JSON.stringify(user));

    this._currentUser$.next(user);
  }

  /** Removes all three auth keys from `localStorage`. */
  private clearStorage(): void {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(AUTH_USER_KEY);
  }

  /** Clears storage, resets the subject, and redirects to `/login`. */
  private clearAndRedirect(): void {
    this.clearStorage();
    this._currentUser$.next(null);
    this.router.navigate(['/login']);
  }
}
