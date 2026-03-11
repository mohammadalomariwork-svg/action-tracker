# Action Tracker - Administrator Guide

> **Version:** 1.0
> **Last Updated:** March 2026
> **Audience:** System Administrators, VP Office Staff, Department Managers

---

## Table of Contents

1. [Overview](#1-overview)
2. [Login & Authentication](#2-login--authentication)
3. [Team Dashboard](#3-team-dashboard)
4. [Management Dashboard](#4-management-dashboard)
5. [Action Items](#5-action-items)
6. [Workspaces](#6-workspaces)
7. [Projects](#7-projects)
8. [Milestones](#8-milestones)
9. [Reports & Export](#9-reports--export)
10. [Admin Panel](#10-admin-panel)
11. [Organization Chart](#11-organization-chart)
12. [Strategic Objectives](#12-strategic-objectives)
13. [KPIs & Targets](#13-kpis--targets)
14. [User Management](#14-user-management)
15. [Comments & Documents](#15-comments--documents)

---

## 1. Overview

Action Tracker is a centralized platform built for the VP Office to track, escalate, and report on action items across all university departments. It supports:

- **Workspaces** tied to organizational units
- **Projects** (Strategic & Operational) with milestones
- **Action Items** with assignees, priorities, statuses, and escalation workflows
- **KPIs & Targets** aligned to strategic objectives
- **Role-based access control** with Azure AD SSO and local accounts
- **Dashboards & Reports** with charts and CSV export

---

## 2. Login & Authentication

The system supports two authentication methods:

### 2.1 System Account Login

Use your registered email and password to sign in.

```
+--------------------------------------------------+
|                  Sign In                         |
|                                                  |
|  System Account Login                            |
|  ┌──────────────────────────────────────────┐    |
|  │ Email address                             │    |
|  │ admin@ku.ac.ae                            │    |
|  └──────────────────────────────────────────┘    |
|  ┌──────────────────────────────────────────┐    |
|  │ Password                            [👁]  │    |
|  │ ••••••••                                  │    |
|  └──────────────────────────────────────────┘    |
|  [ Sign In ]                                     |
|                                                  |
|  ──────────── OR ────────────                    |
|                                                  |
|  Sign in with Microsoft                          |
|  [ 🪟 Continue with Microsoft ]                  |
|                                                  |
|  Khalifa University · Campus Operations          |
+--------------------------------------------------+
```

### 2.2 Microsoft SSO (Azure AD)

Click **"Continue with Microsoft"** to authenticate using your organizational Active Directory account. This is the recommended method for KU staff.

---

## 3. Team Dashboard

**Navigation:** Sidebar > Dashboard

The Team Dashboard is the default landing page after login. It provides a personalized overview of your actions and team activity.

### 3.1 Greeting Banner & Quick Actions

```
+------------------------------------------------------------------+
| Good morning, Ahmed! 👋                                          |
| Here's an overview of your actions and team activity.            |
|                                                                  |
|              [ + New Action ]  [ View All Actions ]              |
|              [ 📊 Management View ]  (privileged users only)     |
+------------------------------------------------------------------+
```

### 3.2 Stats Bar

Displays key metrics at a glance:

```
+----------+  +---------------+  +----------------+  +------------+  +---------+
| 📋 42    |  | ✅ 78%        |  | 📅 85%         |  | 🚨 3       |  | ⏰ 5    |
| Total    |  | Completion    |  | On-Time        |  | Escalations|  | Overdue |
| Actions  |  | Rate          |  | Delivery       |  |            |  |         |
+----------+  +---------------+  +----------------+  +------------+  +---------+
```

### 3.3 My Actions Table

Shows actions assigned to the logged-in user:

```
+------------------------------------------------------------------+
| 📌 My Actions                                   View all →       |
+------------------------------------------------------------------+
| Title                    | Priority | Status      | Due          |
|--------------------------|----------|-------------|--------------|
| Review IT Policy Draft   | 🔴 High  | In Progress | Mar 15       |
| Update Network Firewall  | 🟡 Med   | Open        | Mar 20       |
| Prepare Budget Report    | 🟠 High  | Open        | Mar 22       |
+------------------------------------------------------------------+
```

### 3.4 Team Activity Panel

Shows overdue, due-this-week, and completed counts with a horizontal bar chart of status distribution.

```
+------------------------------------------------------------------+
| 📊 Team Activity                                                 |
|                                                                  |
|  [ 5 Overdue ]  [ 8 Due this week ]  [ 27 Completed ]           |
|                                                                  |
|  Status Distribution (Bar Chart)                                 |
|  Open        ████████████          12                            |
|  In Progress ██████████████████    18                            |
|  Completed   ██████████████████████████████  27                  |
|  Closed      ████                  4                             |
+------------------------------------------------------------------+
```

### 3.5 Recent Items Table

```
+------------------------------------------------------------------+
| 🕐 Recent Items                                  View all →      |
+------------------------------------------------------------------+
| ID     | Title               | Assignee | Priority | Status | Due|
|--------|---------------------|----------|----------|--------|----|
| ACT-42 | Review IT Policy    | Ahmed K. | 🔴 High  | Open   | Mar 15 |
| ACT-41 | Update Firewall     | Sara M.  | 🟡 Med   | InProg | Mar 20 |
| ACT-40 | Budget Report Q1    | Omar H.  | 🟠 High  | Open   | Mar 22 |
+------------------------------------------------------------------+
```

---

## 4. Management Dashboard

**Navigation:** Sidebar > Management Dashboard
**Access:** Privileged users only (Manager, Admin roles)

Provides an executive-level overview of action item performance across the organization.

### 4.1 KPI Cards

```
+----------------+  +----------------+  +------------------+  +------------------+
| ✅ 78%         |  | 📅 85%         |  | 🚨 3             |  | ⚡ 12            |
| Completion     |  | On-Time        |  | Active           |  | Team Velocity    |
| Rate           |  | Delivery       |  | Escalations      |  | (this period)    |
+----------------+  +----------------+  +------------------+  +------------------+
```

### 4.2 Charts Section

**Status Breakdown (Doughnut Chart)**

```
        ╭───────────╮
      ╱   Open: 12   ╲
     │  InProgress:18  │
     │  Completed: 27  │
      ╲  Closed: 4   ╱
        ╰───────────╯
          61 items
```

**Team Workload (Bar Chart)**

Shows action item distribution per team member.

```
  Ahmed K.   ████████████  12
  Sara M.    ██████████    10
  Omar H.    ████████       8
  Fatima A.  ██████         6
  Hassan B.  ████           4
```

### 4.3 At-Risk & Escalated Items

```
+------------------------------------------------------------------+
| ⚠️ At-Risk & Escalated Items                     View all →      |
+------------------------------------------------------------------+
| ACT-38 🚨 Escalated                                              |
| Server Migration Deadline                                         |
|   [AK] Ahmed K.    5d overdue    Critical                        |
|                                                                  |
| ACT-35                                                           |
| Compliance Audit Response                                         |
|   [SM] Sara M.     2d overdue    High                            |
+------------------------------------------------------------------+
```

### 4.4 Recent Activity Timeline

```
+------------------------------------------------------------------+
| 🕐 Recent Activity                                               |
+------------------------------------------------------------------+
|  ● ACT-42 status changed to "In Progress"                       |
|    [AK] Ahmed K.                         Mar 10, 14:30          |
|                                                                  |
|  ● ACT-41 completed                                             |
|    [SM] Sara M.                          Mar 10, 11:15          |
|                                                                  |
|  ● ACT-39 created                                               |
|    [OH] Omar H.                          Mar 9, 16:45           |
+------------------------------------------------------------------+
```

### 4.5 Critical & High Priority Actions Table

```
+----------------------------------------------------------------------+
| 🔴 Critical & High Priority Actions                     3 items      |
+----------------------------------------------------------------------+
| Action ID | Title              | Owner    | Priority | Status | Due        | Escalated |
|-----------|--------------------|----------|----------|--------|------------|-----------|
| ACT-38    | Server Migration   | Ahmed K. | Critical | Open   | Mar 5      | 🚨 Yes    |
| ACT-35    | Compliance Audit   | Sara M.  | High     | InProg | Mar 8      | —         |
| ACT-30    | Budget Approval    | Omar H.  | High     | Open   | Mar 12     | —         |
+----------------------------------------------------------------------+
```

---

## 5. Action Items

### 5.1 Action List

**Navigation:** Sidebar > My Actions

The main listing page for all action items with filtering, sorting, searching, pagination, and CSV export.

**Stats Bar:**

```
+----------+  +----------------+  +---------------+  +-----------+  +---------+
| 📋 42    |  | 🔴 8           |  | ⚙️ 18         |  | ✅ 27     |  | ⏰ 5    |
| Total    |  | Critical/High  |  | In Progress   |  | Completed |  | Overdue |
+----------+  +----------------+  +---------------+  +-----------+  +---------+
```

**Toolbar (Search + Filters):**

```
+------------------------------------------------------------------+
| 🔍 Search by title, ID, assignee…                                |
| [All Priorities ▾]  [All Statuses ▾]  [All Assignees ▾]  [✕ Clear] |
+------------------------------------------------------------------+
```

**Table View:**

```
+----------------------------------------------------------------------+
| ID ↕  | Title ↕           | Project       | Milestone   | Assignee(s)    | Priority ↕ | Status        | Due Date ↕  | Progress | Actions     |
|-------|-------------------|---------------|-------------|----------------|------------|---------------|-------------|----------|-------------|
| ACT-42| Review IT Policy  | IT Upgrade    | Phase 1     | [AK] Ahmed K.  | 🔴 High    | [In Progress] | Mar 15, 2026| ████ 60% | 👁️ ✏️ 🗑️    |
| ACT-41| Update Firewall   | IT Upgrade    | Phase 2     | [SM] Sara M.   | 🟡 Medium  | [Open ▾]      | Mar 20, 2026| ██ 30%   | 👁️ ✏️ 🗑️    |
| ACT-40| Budget Report Q1  | —             | —           | [OH] Omar H.   | 🟠 High    | [Open ▾]      | Mar 22, 2026| █ 10%    | 👁️ ✏️ 🗑️    |
+----------------------------------------------------------------------+
| Showing 1–10 of 42 items       [10/page ▾]  [‹ Prev] 1/5 [Next ›]  |
+----------------------------------------------------------------------+
```

**Key features:**
- **Inline status change:** Click the status badge to change it directly from the table
- **Sortable columns:** ID, Title, Priority, Due Date
- **Card view:** Toggle between table and card view (useful on mobile)
- **CSV Export:** Click "Export CSV" in the page header
- **Escalation indicator:** 🚨 icon next to escalated items

### 5.2 Create / Edit Action Item

**Navigation:** My Actions > + New Action (or click ✏️ Edit on an existing item)

```
+------------------------------------------------------------------+
|  📝 New Action Item                                   [← Cancel] |
|  Fill in the details below to create a new action item.          |
+------------------------------------------------------------------+
|                                                                  |
|  CORE DETAILS                                                    |
|  ┌──────────────────────────────────────────────────────────┐    |
|  │ Title *                                                   │    |
|  │ Review IT Security Policy Draft for Q2                    │    |
|  └──────────────────────────────────────────────────────────┘    |
|  ┌──────────────────────────────────────────────────────────┐    |
|  │ Description                                               │    |
|  │ Review and provide feedback on the updated IT security    │    |
|  │ policy document before the board meeting on March 30.     │    |
|  │                                          245 / 4000       │    |
|  └──────────────────────────────────────────────────────────┘    |
|                                                                  |
|  ASSIGNMENT & CLASSIFICATION                                     |
|  Workspace *        Priority *        Status                     |
|  [VP Office    ▾]   [🔴 High     ▾]   [Open          ▾]         |
|                                                                  |
|  Start Date          Due Date *        Escalation                |
|  [2026-03-10]        [2026-03-25]      [ ] Not escalated         |
|                                                                  |
|  ASSIGNEE(S) *                                                   |
|  Select one or more users to assign this action item to.         |
|  [✓ Ahmed K.] [✓ Sara M.] [ Omar H.] [ Fatima A.]               |
|                                                                  |
|  PROGRESS                                                        |
|  Progress — 60%                                                  |
|  0% ═══════════════════●═══════════════ 100%                     |
|  [████████████████████████░░░░░░░░░░░░░░] 60%                    |
|                                                                  |
|              [Cancel]  [✅ Create Action]                         |
+------------------------------------------------------------------+
```

**Required fields:** Title, Workspace, Priority, Due Date, Assignee(s)

### 5.3 Action Item Detail (View Mode)

**Navigation:** Click 👁️ View on any action item

```
+------------------------------------------------------------------+
| [← Back to Workspace]                              [✏️ Edit]     |
|                                                                  |
| 📋 ACT-42  Review IT Security Policy Draft                      |
|   [Standalone] [🔴 High] [In Progress]                          |
+------------------------------------------------------------------+
|                                                                  |
| ℹ️ Details                                                       |
| ─────────────────────────────────────────────────                |
| Workspace:    VP Office Operations                               |
| Project:      IT Infrastructure Upgrade                          |
| Milestone:    Phase 1 - Assessment                               |
| Priority:     🔴 High                                            |
| Status:       In Progress                                        |
| Progress:     [████████████████░░░░] 60%                         |
| Start Date:   Mar 10, 2026                                      |
| Due Date:     Mar 25, 2026                                      |
| Assignee(s):  [A] Ahmed K.  [S] Sara M.                         |
| Created:      Mar 8, 2026, 9:30 AM                              |
| Last Updated: Mar 10, 2026, 2:15 PM                             |
| Description:  Review and provide feedback on the updated IT...   |
|                                                                  |
| ⚠️ Escalation History                                            |
| ─────────────────────────────────────────────────                |
| Ahmed K.                            Mar 9, 2026, 11:00 AM       |
| Escalated due to approaching board meeting deadline and          |
| pending approvals from legal department.                         |
|                                                                  |
| 💬 Comments                                                      |
| ─────────────────────────────────────────────────                |
| (See Section 15)                                                 |
|                                                                  |
| 📎 Documents                                                     |
| ─────────────────────────────────────────────────                |
| (See Section 15)                                                 |
+------------------------------------------------------------------+
```

### 5.4 Escalation Workflow

When editing an action item, toggle the **Escalation** switch to mark it as escalated:

- An **Escalation Explanation** (required) must be provided
- The escalation is logged with the user's name and timestamp
- Escalated items appear with a 🚨 badge throughout the system
- Previous escalation history is preserved and displayed

---

## 6. Workspaces

**Navigation:** Sidebar > Workspaces

Workspaces group projects and standalone action items under an organizational unit.

### 6.1 Workspace List

```
+----------------------------------------------------------------------+
| 🏢 Workspaces                                    [+ New Workspace]    |
| Manage and organize team workspaces across your organization units.  |
+----------------------------------------------------------------------+

Summary Cards:
+------+  +--------+  +----------+  +-------------+  +----------+  +---------+
| 8    |  | 6      |  | 12       |  | 8           |  | 24       |  | 18      |
| Total|  | Active |  | Strategic|  | Operational |  | Standaln |  | Project |
| WS   |  | WS     |  | Projects |  | Projects    |  | Actions  |  | Actions |
+------+  +--------+  +----------+  +-------------+  +----------+  +---------+
+----------+  +----------+  +-----------+  +----------+
| 42       |  | 78%      |  | 85%       |  | 72%      |
| Strategic|  | Project  |  | Project   |  | Standaln |
| Actions  |  | Complete |  | On-Time   |  | Complete |
+----------+  +----------+  +-----------+  +----------+

+----------------------------------------------------------------------+
| 🔍 Search workspaces...                Showing 6 workspaces          |
+----------------------------------------------------------------------+
| TITLE                  | ORG UNIT           | ADMINS         | STATUS  | ACTIONS        |
|------------------------|--------------------|----------------|---------|----------------|
| [VP] VP Office Ops     | VP Office          | [AK] Ahmed K.  | ● Active| View Edit Del  |
|  Created Jan 15 · 12   | 🏢 VP Office       | (IT Dept)      |         |                |
|  actions open          |                    |                |         |                |
|                        |                    |                |         |                |
| [IT] IT Department     | Information Tech   | [SM] Sara M.   | ● Active| View Edit Del  |
|  Created Feb 1 · 8     | 🏢 IT Dept         | (IT Dept)      |         |                |
|  actions open          |                    |                |         |                |
+----------------------------------------------------------------------+
| Showing 1–6 of 6 workspaces                                         |
+----------------------------------------------------------------------+
```

### 6.2 Workspace Detail

Clicking **View** opens the workspace detail page showing:

- **Header:** Workspace title, organization unit, active status
- **Statistics:** Project count, action item count, completion rates
- **Details grid:** Description, admins, created/updated dates
- **Projects tab:** List of all projects within this workspace with inline add/edit
- **Standalone Actions tab:** Action items not linked to any project
- **Comments & Documents:** Attached to the workspace

### 6.3 Create / Edit Workspace

Use the **New Workspace** button or **Edit** action to open the workspace form:

**Fields:**
- **Title** (required)
- **Description**
- **Organization Unit** (required, selected from the org chart)
- **Admin Users** (multi-select from registered users)
- **Is Active** toggle

---

## 7. Projects

**Navigation:** Workspaces > [Workspace] > Projects tab

Projects live inside workspaces and can be either **Strategic** (linked to a strategic objective) or **Operational**.

### 7.1 Project Detail

```
+----------------------------------------------------------------------+
| [← Back to Workspace]                                  [✏️ Edit]     |
|                                                                      |
| 📊 PRJ-001  IT Infrastructure Upgrade                               |
|   [Operational] [🔴 High] [In Progress] [🔒 Baselined]              |
+----------------------------------------------------------------------+
|                                                                      |
| Stats:                                                               |
| +----------+  +----------+  +----------+  +----------+  +----------+ |
| | 5        |  | 12       |  | 65%      |  | 80%      |  | 2        | |
| | Milestns |  | Actions  |  | Complete |  | On-Time  |  | Escalatd | |
| +----------+  +----------+  +----------+  +----------+  +----------+ |
|                                                                      |
| 📁 Project Details                                                   |
| ─────────────────────────────────────────────────                    |
| Workspace:       VP Office Operations                                |
| Type:            Operational                                         |
| Priority:        🔴 High                                             |
| Status:          In Progress                                         |
| Project Manager: [A] Ahmed K.                                        |
| Sponsors:        [S] Sara M.  [O] Omar H.                           |
| Planned Start:   Jan 15, 2026                                       |
| Planned End:     Jun 30, 2026                                       |
| Actual Start:    Jan 20, 2026                                       |
| Budget:          250,000.00 AED                                      |
| Baselined:       🔒 Yes — Dates locked                               |
| Currency:        AED                                                 |
| Created:         Jan 10, 2026, 10:00 AM                             |
| Last Updated:    Mar 8, 2026, 3:45 PM                               |
| Description:     Comprehensive upgrade of the university's IT...     |
|                                                                      |
| (Milestones section — see Section 8)                                 |
| (Comments section)                                                   |
| (Documents section)                                                  |
+----------------------------------------------------------------------+
```

### 7.2 Create / Edit Project

The project form (offcanvas drawer) includes:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Project Name | Text | Yes | Max 255 characters |
| Description | Textarea | No | |
| Project Type | Toggle | Yes | Operational or Strategic |
| Strategic Objective | Dropdown | Yes (if Strategic) | Loaded from objectives for the workspace's org unit |
| Priority | Dropdown | Yes | Critical, High, Medium, Low |
| Project Manager | Dropdown | Yes | Select from registered users |
| Sponsor(s) | Multi-select | Yes | At least one sponsor required |
| Planned Start | Date | Yes | |
| Planned End | Date | Yes | Must be after start date |
| Approved Budget | Number | No | In workspace currency (AED) |
| Status | Dropdown | No | Draft, Planning, In Progress, Completed, etc. |
| Actual Start | Date | No | |

---

## 8. Milestones

**Navigation:** Project Detail > Milestones Section

Milestones break projects into phases with their own action items.

### 8.1 Milestone List (within Project)

```
+----------------------------------------------------------------------+
| 📌 Milestones                                    [+ Add Milestone]    |
+----------------------------------------------------------------------+
| Phase 1 - Assessment                                                 |
|   Status: Completed  |  Due: Feb 28, 2026  |  3 actions             |
|   [View] [Edit] [Delete]                                            |
|                                                                      |
| Phase 2 - Implementation                                            |
|   Status: In Progress  |  Due: Apr 30, 2026  |  5 actions           |
|   [View] [Edit] [Delete]                                            |
|                                                                      |
| Phase 3 - Testing & Go-Live                                         |
|   Status: Not Started  |  Due: Jun 30, 2026  |  4 actions           |
|   [View] [Edit] [Delete]                                            |
+----------------------------------------------------------------------+
```

### 8.2 Milestone Detail

Shows milestone details, associated action items, and allows adding new action items directly to the milestone.

---

## 9. Reports & Export

**Navigation:** Sidebar > Reports

### 9.1 Export Action Items

Apply filters and download action items as a CSV file.

```
+----------------------------------------------------------------------+
| ⬇️ Export Action Items                                               |
| Apply optional filters then download as CSV                          |
+----------------------------------------------------------------------+
| Status          Priority        Assignee                             |
| [All Statuses▾] [All Priorities▾] [All Assignees▾]                  |
|                                                                      |
| Due Date From     Due Date To                                        |
| [2026-01-01]      [2026-03-31]                                       |
|                                                                      |
|                    [✕ Clear Filters]  [⬇ Export to CSV]              |
+----------------------------------------------------------------------+
```

### 9.2 Summary Statistics

```
+----------------------------------------------------------------------+
| 📊 Summary Statistics                                                |
| Aggregate performance metrics                                        |
+----------------------------------------------------------------------+
| +--------+  +---------+  +---------+  +----------+                   |
| | 📋 42  |  | ✅ 78%  |  | 📅 85%  |  | 🚨 3     |                   |
| | Total  |  | Complet.|  | On-Time |  | Escalatns|                   |
| +--------+  +---------+  +---------+  +----------+                   |
| +--------+  +---------+  +---------+  +----------+                   |
| | ⏰ 5   |  | 🔴 8    |  | ⚙️ 18   |  | ⚡ 12    |                   |
| | Overdue|  | Crit/Hi |  | In Prog |  | Velocity |                   |
| +--------+  +---------+  +---------+  +----------+                   |
|                                                                      |
| Status Distribution:                                                 |
| Open         ████████████             12 (20%)                       |
| In Progress  ████████████████████     18 (29%)                       |
| Completed    ████████████████████████████████  27 (44%)              |
| Closed       ████                      4 (7%)                        |
+----------------------------------------------------------------------+
```

### 9.3 Charts

**Actions by Category (Bar Chart)** — Distribution across departments
**Actions by Priority (Doughnut Chart)** — Breakdown by urgency level (Critical, High, Medium, Low)

---

## 10. Admin Panel

**Navigation:** Sidebar > Admin Panel
**Access:** Admin role required

The Admin Panel is the central hub for system configuration.

```
+----------------------------------------------------------------------+
| 🛡️ Admin Panel                                                       |
| System Administration                                                |
+----------------------------------------------------------------------+
|                                                                      |
| +---------------------------+  +---------------------------+         |
| | 📊 Organization Chart     |  | 🎯 Strategic Objectives   |         |
| | Manage KU org units up    |  | Define and assign          |         |
| | to 10 hierarchy levels.   |  | strategic objectives       |         |
| | Build your institutional  |  | (SO-1, SO-2…) to          |         |
| | structure.                |  | organizational units.      |         |
| |         [Manage →]        |  |         [Manage →]         |         |
| +---------------------------+  +---------------------------+         |
|                                                                      |
| +---------------------------+  +---------------------------+         |
| | 📈 KPIs & Targets         |  | 👥 User Management        |         |
| | Create KPIs per objective |  | Manage system users,       |         |
| | with monthly, quarterly,  |  | roles, and access control  |         |
| | or annual measurement     |  | for both local and AD      |         |
| | targets.                  |  | accounts.                  |         |
| |         [Manage →]        |  |         [Manage →]         |         |
| +---------------------------+  +---------------------------+         |
+----------------------------------------------------------------------+
```

---

## 11. Organization Chart

**Navigation:** Admin Panel > Organization Chart

Manage the institutional hierarchy with up to 10 levels of nesting.

### 11.1 Tree View

```
+----------------------------------------------------------------------+
| 📊 Organization Chart                             [+ Add Org Unit]    |
| Manage organization structure and units                              |
+----------------------------------------------------------------------+
| [ ] Show deleted    [Expand All] [Collapse All]                      |
+----------------------------------------------------------------------+
|                                                                      |
| ▼ [L1] [KU] Khalifa University                      [+ ✏️ 🗑️]       |
|   ▼ [L2] [VPO] VP Office                            [+ ✏️ 🗑️]       |
|     ▼ [L3] [IT] Information Technology               [+ ✏️ 🗑️]       |
|       ► [L4] [NET] Network Infrastructure            [+ ✏️ 🗑️]       |
|       ► [L4] [SEC] Cybersecurity                     [+ ✏️ 🗑️]       |
|       ► [L4] [DEV] Application Development           [+ ✏️ 🗑️]       |
|     ► [L3] [FM] Facilities Management                [+ ✏️ 🗑️]       |
|     ► [L3] [HR] Human Resources                      [+ ✏️ 🗑️]       |
|   ► [L2] [ACA] Academic Affairs                      [+ ✏️ 🗑️]       |
|   ► [L2] [RES] Research & Innovation                 [+ ✏️ 🗑️]       |
|                                                                      |
+----------------------------------------------------------------------+
```

### 11.2 Add / Edit Org Unit

The form appears as a side panel when adding or editing:

| Field | Description |
|-------|-------------|
| **Name** | Unit name (e.g., "Information Technology") |
| **Code** | Short code (e.g., "IT") |
| **Parent Unit** | Auto-populated based on where you clicked "+" |
| **Level** | Auto-calculated (1–10) |

**Actions available per node:**
- **Add child** (+) — Add a sub-unit (up to level 10)
- **Edit** (✏️) — Modify name/code
- **Delete** (🗑️) — Soft-delete (can be restored)
- **Restore** (↺) — Visible when "Show deleted" is toggled on

**Audit trail:** Each node shows Created By, Updated By, and Deleted By information on hover.

---

## 12. Strategic Objectives

**Navigation:** Admin Panel > Strategic Objectives

Define strategic objectives that can be linked to projects.

### 12.1 Objectives List

```
+----------------------------------------------------------------------+
| 🎯 Strategic Objectives                          [+ Add Objective]    |
| Define and manage strategic objectives                               |
+----------------------------------------------------------------------+
| Org Unit: [All Org Units ▾]  🔍 Search by code or statement…        |
|                                              [ ] Show deleted        |
+----------------------------------------------------------------------+
| Code   | Statement                        | Org Unit    | KPIs | Status  | Created     | Actions      |
|--------|----------------------------------|-------------|------|---------|-------------|--------------|
| SO-1   | Enhance research output and      | VP Office   |  3   | Active  | 15 Jan 2026 | ✏️ 🗑️ 📋     |
|        | innovation capabilities          |             |      |         |             |              |
| SO-2   | Improve student experience and   | Academic    |  2   | Active  | 15 Jan 2026 | ✏️ 🗑️ 📋     |
|        | academic excellence              | Affairs     |      |         |             |              |
| SO-3   | Strengthen institutional         | VP Office   |  4   | Active  | 20 Jan 2026 | ✏️ 🗑️ 📋     |
|        | governance and accountability    |             |      |         |             |              |
| SO-4   | Optimize campus operations and   | Facilities  |  1   | Active  | 25 Jan 2026 | ✏️ 🗑️ 📋     |
|        | resource utilization             | Mgmt        |      |         |             |              |
+----------------------------------------------------------------------+
| ← Previous    Page 1 of 1 (4 total)    Next →                       |
+----------------------------------------------------------------------+
```

### 12.2 Add / Edit Objective

Opens in an offcanvas drawer:

| Field | Type | Required |
|-------|------|----------|
| Objective Code | Text | Yes (e.g., SO-5) |
| Statement | Textarea | Yes |
| Organization Unit | Dropdown | Yes |

### 12.3 View KPIs

Click the 📋 button to navigate to the KPIs list filtered by the selected objective.

---

## 13. KPIs & Targets

**Navigation:** Admin Panel > KPIs & Targets (or via Strategic Objectives > View KPIs)

### 13.1 KPI List

```
+----------------------------------------------------------------------+
| 📈 KPIs & Targets                                     [+ Add KPI]    |
| Track key performance indicators                                     |
+----------------------------------------------------------------------+
| Objective: [All Objectives ▾]  🔍 Search by KPI name…               |
|                                              [ ] Show deleted        |
+----------------------------------------------------------------------+
| #              | Name                     | Objective | Period    | Unit | Targets | Status | Actions       |
|----------------|--------------------------|-----------|-----------|------|---------|--------|---------------|
| SO-1-KPI-001   | Research publications     | SO-1      | Quarterly | Count|  4      | Active | ✏️ 🗑️ 📊      |
| SO-1-KPI-002   | Patent filings            | SO-1      | Annual    | Count|  1      | Active | ✏️ 🗑️ 📊      |
| SO-2-KPI-001   | Student satisfaction      | SO-2      | Annual    | %    |  1      | Active | ✏️ 🗑️ 📊      |
| SO-3-KPI-001   | Audit compliance rate     | SO-3      | Quarterly | %    |  4      | Active | ✏️ 🗑️ 📊      |
+----------------------------------------------------------------------+
| ← Previous    Page 1 of 1 (4 total)    Next →                       |
+----------------------------------------------------------------------+
```

### 13.2 Add / Edit KPI

Opens in an offcanvas drawer:

| Field | Type | Required |
|-------|------|----------|
| KPI Name | Text | Yes |
| Strategic Objective | Dropdown | Yes |
| Measurement Period | Dropdown | Yes (Monthly, Quarterly, Annual) |
| Unit of Measure | Text | No (e.g., %, Count, AED) |

### 13.3 Manage Targets

Click the 📊 button to open the KPI Targets management view. Targets define the expected values per measurement period.

| Field | Description |
|-------|-------------|
| Period Label | e.g., "Q1 2026", "January 2026", "FY 2026" |
| Target Value | Numeric goal for this period |
| Actual Value | Recorded actual performance |
| Notes | Optional comments |

---

## 14. User Management

**Navigation:** Admin Panel > User Management

### 14.1 User List

```
+----------------------------------------------------------------------+
| 👥 User Management                                                   |
| Manage users, roles and permissions                                  |
|              [🪪 Add AD User / Search Employee]  [+ Add External User]|
+----------------------------------------------------------------------+
| 🔍 Search by name, email or username…                                |
+----------------------------------------------------------------------+
| Full Name    | Email              | Username    | Roles    | Org Unit | AD   | Status | Actions                   |
|--------------|--------------------|-----------  |----------|----------|------|--------|---------------------------|
| Ahmed K.     | ahmed@ku.ac.ae     | ahmed.k     | Admin    | VP Office| AD   | Active | [Change Role] [Unit] [Deactivate] |
| Sara M.      | sara@ku.ac.ae      | sara.m      | Manager  | IT Dept  | AD   | Active | [Change Role] [Unit] [Deactivate] |
| Omar H.      | omar@ku.ac.ae      | omar.h      | User     | Facilities| AD  | Active | [Change Role] [Unit] [Deactivate] |
| Fatima A.    | fatima@ku.ac.ae    | fatima.a    | User     | HR       | AD   | Active | [Change Role] [Unit] [Deactivate] |
| External Co. | vendor@external.com| ext.vendor  | User     | —        | Local| Active | [Change Role] [Unit] [Deactivate] |
+----------------------------------------------------------------------+
| ← Previous    Page 1 of 1 (5 users)    Next →                       |
+----------------------------------------------------------------------+
```

### 14.2 User Actions

| Action | Description |
|--------|-------------|
| **Change Role** | Inline dropdown to assign a role (Admin, Manager, User) |
| **User Unit** | Inline dropdown to assign/change the user's organization unit |
| **Deactivate** | Soft-disable the user account (reversible) |
| **Activate** | Re-enable a deactivated account |

### 14.3 Add AD User / Search Employee

**Navigation:** User Management > Add AD User

Search for employees in the Azure Active Directory and register them in the system:

- Search by name or email
- Select from search results
- User is automatically provisioned with AD credentials

### 14.4 Add External User

**Navigation:** User Management > Add External User

Register users who don't have a KU Active Directory account:

| Field | Required |
|-------|----------|
| Full Name | Yes |
| Email | Yes |
| Username | Yes |
| Password | Yes (min 6 characters) |
| Role | Yes |
| Organization Unit | No |

---

## 15. Comments & Documents

Comments and documents can be attached to **Action Items**, **Projects**, and **Workspaces**.

### 15.1 Comments Section

```
+----------------------------------------------------------------------+
| 💬 Comments                                                          |
+----------------------------------------------------------------------+
| ┌──────────────────────────────────────────────────────────────┐     |
| │ Add a comment…                                               │     |
| │                                                              │     |
| └──────────────────────────────────────────────────────────────┘     |
|                                              [Post Comment]          |
|                                                                      |
| [A] Ahmed K.                              Mar 10, 2026, 2:30 PM    |
| I've reviewed the initial draft. The network topology section needs  |
| more detail on the failover procedures.                              |
|                                                        [Edit] [Del]  |
|                                                                      |
| [S] Sara M.                               Mar 9, 2026, 11:00 AM    |
| Shared the updated policy document with the legal team for review.   |
|                                                        [Edit] [Del]  |
+----------------------------------------------------------------------+
```

### 15.2 Documents Section

```
+----------------------------------------------------------------------+
| 📎 Documents                                                         |
+----------------------------------------------------------------------+
| [Choose File]  [Upload]                                              |
|                                                                      |
| File Name                    | Uploaded By | Date           | Action |
|------------------------------|-------------|----------------|--------|
| IT_Security_Policy_v2.pdf    | Ahmed K.    | Mar 8, 2026    | ⬇ 🗑️   |
| Network_Topology_Diagram.png | Sara M.     | Mar 7, 2026    | ⬇ 🗑️   |
| Budget_Estimate_Q2.xlsx      | Omar H.     | Mar 5, 2026    | ⬇ 🗑️   |
+----------------------------------------------------------------------+
```

---

## Appendix A: Role Permissions

| Feature | Admin | Manager | User |
|---------|-------|---------|------|
| View Dashboard | Yes | Yes | Yes |
| Management Dashboard | Yes | Yes | No |
| Create/Edit Actions | Yes | Yes | Yes |
| Delete Actions | Yes | Yes | Own only |
| Create/Edit Projects | Yes | Yes | No |
| Manage Workspaces | Yes | Yes | No |
| Admin Panel | Yes | No | No |
| Organization Chart | Yes | No | No |
| Strategic Objectives | Yes | No | No |
| KPIs & Targets | Yes | No | No |
| User Management | Yes | No | No |
| Reports & Export | Yes | Yes | Yes |

## Appendix B: Status Workflow

**Action Item Statuses:**

```
Open → In Progress → Completed → Closed
                  ↘ On Hold ↗
```

**Project Statuses:**

```
Draft → Planning → In Progress → Completed → Closed
                              ↘ On Hold ↗
```

## Appendix C: Priority Levels

| Priority | Icon | Color | Description |
|----------|------|-------|-------------|
| Critical | 🔴 | Red | Immediate attention required |
| High | 🟠 | Orange | Should be addressed soon |
| Medium | 🟡 | Yellow | Normal priority |
| Low | 🟢 | Green | Can be addressed when convenient |

## Appendix D: Keyboard Shortcuts & Tips

- **Table sorting:** Click any column header with ↕ to sort ascending/descending
- **Quick status change:** Click the status badge directly in the action list table
- **Card view:** Toggle the grid icon in the action list toolbar for mobile-friendly cards
- **Search:** All list pages support real-time text search
- **Pagination:** Adjust page size (10, 25, 50 per page) at the bottom of tables

---

*This guide covers all features of the Action Tracker system as of version 1.0. For technical architecture details, refer to `docs/architecture.md`.*
