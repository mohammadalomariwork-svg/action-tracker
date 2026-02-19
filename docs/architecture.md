# Action Tracker  Architecture

## Stack
- Backend: .NET 9, ASP.NET Core, EF Core 9, ASP.NET Identity, JWT
- Frontend: Angular 20, Bootstrap 5, SCSS, MSAL, ApexCharts
- Database: SQL Server (server: mkocenter, db: ActionTracker)
- Auth: Azure AD SSO + own JWT issuance

## Solution Structure (backend/)
- ActionTracker.Domain       (entities, enums, exceptions)
- ActionTracker.Application  (services, DTOs, validators)
- ActionTracker.Infrastructure (EF Core, repos, services)
- ActionTracker.API           (controllers, middleware, startup)

## Conventions
- All service methods async with CancellationToken
- Route prefix: /api/v1/[resource]
- Responses: ApiResponse<T> wrapper
- IDs: Guid (not int)
- Soft delete: IsDeleted flag (never physical delete)
- DateTime: always UTC stored, convert to UAE (UTC+4) for display
