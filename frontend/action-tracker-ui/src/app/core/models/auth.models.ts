/**
 * Request payload for authenticating a locally registered user
 * using their email address and password.
 */
export interface LoginRequest {
  /** The user's registered email address. Used as the login identifier. */
  email: string;

  /** The user's plain-text password. Validated server-side against the stored hash. */
  password: string;
}

/**
 * Request payload for authenticating a user who has already completed
 * the Microsoft Entra ID (Azure AD) interactive login on the frontend.
 * The access token is obtained from MSAL and then exchanged for
 * application-level tokens by the API.
 */
export interface AzureAdLoginRequest {
  /** The access token obtained from Microsoft Entra ID via MSAL. */
  accessToken: string;
}

/**
 * Unified authentication response returned by the API after a successful
 * login, regardless of whether the user authenticated locally or via Azure AD.
 * Contains the tokens required for subsequent API calls and the essential
 * identity information needed by the frontend.
 */
export interface AuthResponse {
  /** Short-lived JWT to be sent as a Bearer token in the Authorization header. */
  accessToken: string;

  /** Long-lived opaque token used to obtain a new access token without re-login. */
  refreshToken: string;

  /** ISO 8601 UTC timestamp at which the access token expires. */
  accessTokenExpiry: string;

  /** The authenticated user's unique identifier (AspNetUsers.Id). */
  userId: string;

  /** The authenticated user's email address. */
  email: string;

  /**
   * The name to display in the UI. Sourced from the user's DisplayName if set,
   * otherwise derived from FirstName + LastName.
   */
  displayName: string;

  /**
   * Identifies how the user authenticated.
   * `"Local"` for username/password accounts; `"AzureAD"` for federated accounts.
   */
  loginProvider: string;

  /** List of application roles assigned to the user (e.g. "Admin", "Manager"). */
  roles: string[];
}

/**
 * Request payload for exchanging an expired access token and a valid
 * refresh token for a new token pair, without requiring re-authentication.
 */
export interface RefreshTokenRequest {
  /**
   * The expired (or near-expiry) access token. Provided so the server can
   * identify the user and session the refresh request belongs to.
   */
  accessToken: string;

  /**
   * The long-lived refresh token issued during the original login.
   * Must not be revoked or expired.
   */
  refreshToken: string;
}

/**
 * Represents the currently authenticated user as held in the application's
 * client-side auth state (e.g. in an AuthService signal or BehaviorSubject).
 * Derived from the `AuthResponse` received after a successful login.
 */
export interface AuthUser {
  /** The user's unique identifier (AspNetUsers.Id). */
  userId: string;

  /** The user's email address. */
  email: string;

  /** Display name shown in the UI. */
  displayName: string;

  /**
   * Identifies how the user authenticated.
   * `"Local"` for username/password accounts; `"AzureAD"` for federated accounts.
   */
  loginProvider: string;

  /** List of application roles assigned to the user. */
  roles: string[];

  /** Whether the user is currently authenticated (has a valid session). */
  isAuthenticated: boolean;
}
