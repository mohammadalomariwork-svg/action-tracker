# Prompt: CR-001-G — KPI Target Extended Details

> **CR reference:** `CR-001-Strategic-Plans-and-KPI-Actuals.docx`, section CR-001-G
> **Phase:** 1 (Structure)
> **Depends on:** nothing — parallel-safe with CR-001-A
> **Blocks:** CR-001-F (KPI Actuals) reads `Direction` to compute achievement %

---

## 0. Read first

1. `CLAUDE.md`
2. `BACKEND_OVERVIEW.md` — `KpiTarget` entity table
3. `backend/ActionTracker/ActionTracker.Domain/Entities/KpiTarget.cs`
4. `backend/ActionTracker/ActionTracker.Application/Kpis/` — existing target
   commands and the bulk-upsert handler
5. `frontend/action-tracker-ui/src/app/features/kpis/` — the inline-editable
   target rows

---

## 1. Open question — default if unanswered

**Q8: "Target details" field list.** Default applied: add the four fields
below. If the requestor wants different fields, stop and update this prompt.

---

## 2. Scope

Add four new fields to `KpiTarget` to capture the descriptive metadata
implied by "target details" in the source notes. **No actuals work** — that
is CR-001-F.

---

## 3. Domain layer

### 3.1 New enum `Enums/KpiDirection.cs`

```csharp
public enum KpiDirection
{
    HigherIsBetter = 0,  // Default — most KPIs aim higher
    LowerIsBetter = 1,   // e.g. defect rates, response times
    OnTarget = 2          // Hit the target exactly; band tolerance handled in UI
}
```

### 3.2 Modify `Entities/KpiTarget.cs`

Add (all nullable to keep existing rows valid):

| Field | Type | Notes |
|---|---|---|
| `Baseline` | `decimal?` | Reference value used to compute progress |
| `DataSource` | `string?` | Free text up to 500 chars (e.g. "BI Dashboard / SLA Tracker") |
| `CalculationNotes` | `string?` | Up to 2000 chars — target-specific calc notes |
| `Direction` | `KpiDirection` | **Non-nullable** with default `HigherIsBetter`; existing rows backfilled to `HigherIsBetter` in the migration |

`Direction` lives on `KpiTarget` rather than `Kpi` because the requestor's
notes mention "target details" specifically — and different periods could
in principle have different directions if the underlying KPI changes
character year-over-year. If the requestor confirms it's truly KPI-level,
move the field up to `Kpi` and re-run.

---

## 4. Application layer

### 4.1 DTOs

Update existing target DTOs (`KpiTargetDto`, `UpsertKpiTargetDto`,
`BulkUpsertKpiTargetsDto`) to include the four new fields. Keep them
optional in the DTO so existing API consumers don't break.

### 4.2 Validators

- `Baseline`: optional; if present, no range constraint (KPIs vary widely).
- `DataSource`: ≤ 500 chars.
- `CalculationNotes`: ≤ 2000 chars.
- `Direction`: must be a defined enum value.

### 4.3 Bulk upsert

`BulkUpsertKpiTargetsCommandHandler` already exists. Extend it so the new
fields flow through. **Behavioural rule:** if a field is `null` in the
request, **do not overwrite** the existing value (treat null as "no change").
This protects power-users who only edit `TargetValue` from accidentally
wiping their `CalculationNotes`. Document this in a code comment.

---

## 5. Infrastructure layer

### 5.1 EF configuration

`KpiTargetConfiguration`:

- `DataSource`: `HasMaxLength(500)`.
- `CalculationNotes`: `HasMaxLength(2000)`.
- `Direction`: stored as `int`, default value `0`.
- `Baseline`: `decimal(18,4)` (match existing decimal precision in this project).

### 5.2 Migration

```bash
dotnet ef migrations add Add_KpiTarget_Details --project ActionTracker.Infrastructure --startup-project ActionTracker.API
```

Verify:
- The migration adds four nullable columns plus `Direction` with default `0`.
- Existing rows survive untouched and read back with `Direction = HigherIsBetter`.

---

## 6. API layer

No new endpoints — existing `/api/kpis/targets/upsert` and `/bulk-upsert`
just accept the new fields. Update the Swagger DTO comments so the new
fields are documented.

---

## 7. Frontend

### 7.1 Models

Update `src/app/models/kpi.ts` (or equivalent):

```ts
export enum KpiDirection {
  HigherIsBetter = 0,
  LowerIsBetter = 1,
  OnTarget = 2,
}

export interface KpiTarget {
  // ... existing fields ...
  baseline?: number | null;
  dataSource?: string | null;
  calculationNotes?: string | null;
  direction: KpiDirection;
}
```

### 7.2 Inline-editable target rows

Locate the existing target editor (per `FRONTEND_OVERVIEW.md`, the inline
row component with year navigation and Save All). Add a **details panel** —
either an expandable row drawer or a popover — exposing the four new fields.
Do **not** clutter the main grid; this metadata is set occasionally, not
every period.

Suggested UX:

- A small "details" icon on each row.
- Clicking opens a side panel with: Baseline, Data Source, Calculation
  Notes (textarea), Direction (radio or segmented control).
- Save propagates to the existing bulk-upsert call.
- Achievement % calculation in the row should now respect `direction`:
  - `HigherIsBetter`: `actual / target * 100`
  - `LowerIsBetter`: `target / actual * 100` (clamp denom)
  - `OnTarget`: `100 - abs((actual - target) / target * 100)`, clamped 0–100

(The actuals are still entered the old way for now — CR-001-F replaces this.)

### 7.3 Theme

Verify dark and light mode for the new panel and direction control.

---

## 8. Tests

- Unit test `BulkUpsertKpiTargetsCommandHandler` to confirm null-means-no-change
  behaviour. Existing tests must keep passing.
- Frontend: a quick test that the achievement % helper returns the right
  value for each `KpiDirection`.

---

## 9. Acceptance criteria

1. Migration applies cleanly. Existing `KpiTarget` rows are unchanged in
   meaning (read back with `Direction = HigherIsBetter`, others null).
2. POST/PUT through the existing endpoints accepts and persists the new
   fields.
3. The achievement % colouring on the existing target grid behaves correctly
   for each direction (test with `LowerIsBetter` — it must invert).
4. A field left null in a partial update is preserved, not nulled out.

---

## 10. Documentation updates

- `status.md` — under section 6 (KPIs), note "Per-target details: baseline,
  data source, calculation notes, direction (HigherIsBetter / LowerIsBetter /
  OnTarget)". Bump `Last updated:`.
- `BACKEND_OVERVIEW.md` — extend the `KpiTarget` entity table with the new
  fields and add `KpiDirection` to the enums section. Bump `Last updated:`.
- `FRONTEND_OVERVIEW.md` — note the new details panel under the KPIs feature.
  Bump `Last updated:`.

---

## 11. Stop conditions

Stop and ask the user if:

- The requestor confirms `Direction` should live on `Kpi`, not `KpiTarget`
  (changes the migration).
- The schema diff shows existing `Baseline` / `DataSource` columns from
  another developer's branch.
