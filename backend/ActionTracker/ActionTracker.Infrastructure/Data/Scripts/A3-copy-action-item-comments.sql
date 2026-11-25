/* ============================================================================
   WO-2026-11 A-3 — copy action-item comments from Comments to ActionItemComments
   ============================================================================

   What it does
   ------------
   Copies every comment on an action item (dbo.Comments, RelatedEntityType =
   'ActionItem') that is not yet in dbo.ActionItemComments into that table as a
   SUBMITTED action update (Status = 1, SubmittedAt = the comment's CreatedAt),
   keeping its Id, text, author, high-importance flag and dates.

   This is the same INSERT as step 2 of the migration Add_ActionItemUpdates
   (a unit test keeps the two identical). Run it again whenever comments may
   have been written to dbo.Comments by an API older than A-3; each run copies
   only the rows that are still missing.

   Safe to re-run
   --------------
   * Nothing in dbo.Comments is changed or deleted.
   * A row whose Id is already in dbo.ActionItemComments is skipped (the NOT
     EXISTS guard), so a second run inserts 0 rows. A comment edited in
     dbo.Comments after it was copied is NOT copied again.
   * Comments whose action item no longer exists (no row in dbo.ActionItems)
     are skipped: they cannot meet the foreign key. Soft-deleted action items
     do exist, so their comments are copied (and stay hidden with the item).

   Needs
   -----
   The migration Add_ActionItemUpdates applied (the Status, SubmittedAt,
   SubItemId and IsDeleted columns). Without it the INSERT fails with
   "Invalid column name" and nothing is written.

   How to run
   ----------
   1. Run PART 1 (read-only) and review the list.
   2. Run PART 2. It prints how many rows it inserted.
   ========================================================================= */

SET NOCOUNT ON;

/* ---------------------------------------------------------------------------
   PART 1 — PREVIEW (read-only): what PART 2 would copy, and what it skips
   ------------------------------------------------------------------------ */

-- Rows PART 2 would insert.
SELECT  c.Id                    AS CommentId,
        c.RelatedEntityId       AS ActionItemId,
        a.ActionId,
        a.IsDeleted             AS ActionItemIsDeleted,
        c.AuthorUserId,
        c.IsHighImportance,
        c.CreatedAt,
        c.UpdatedAt,
        LEN(c.Content)          AS ContentLength,
        LEFT(c.Content, 80)     AS ContentStart
FROM dbo.Comments AS c
INNER JOIN dbo.ActionItems AS a ON a.Id = c.RelatedEntityId
WHERE c.RelatedEntityType = N'ActionItem'
  AND NOT EXISTS (SELECT 1 FROM dbo.ActionItemComments AS x WHERE x.Id = c.Id)
ORDER BY c.CreatedAt;

-- Summary: to copy / already copied / skipped because the action item is gone.
SELECT
    SUM(CASE WHEN a.Id IS NOT NULL AND x.Id IS NULL THEN 1 ELSE 0 END) AS WouldCopy,
    SUM(CASE WHEN x.Id IS NOT NULL                  THEN 1 ELSE 0 END) AS AlreadyInActionItemComments,
    SUM(CASE WHEN a.Id IS NULL     AND x.Id IS NULL THEN 1 ELSE 0 END) AS SkippedNoActionItem,
    COUNT(*)                                                           AS ActionItemCommentsInComments
FROM dbo.Comments AS c
LEFT JOIN dbo.ActionItems        AS a ON a.Id = c.RelatedEntityId
LEFT JOIN dbo.ActionItemComments AS x ON x.Id = c.Id
WHERE c.RelatedEntityType = N'ActionItem';

/* ---------------------------------------------------------------------------
   PART 2 — COPY (writes to dbo.ActionItemComments only)
   ------------------------------------------------------------------------ */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- <copy>
INSERT INTO dbo.ActionItemComments
    (Id, ActionItemId, SubItemId, Content, AuthorUserId, IsHighImportance,
     CreatedAt, UpdatedAt, Status, SubmittedAt, IsDeleted)
SELECT c.Id, c.RelatedEntityId, NULL, c.Content, c.AuthorUserId, c.IsHighImportance,
       c.CreatedAt, c.UpdatedAt, 1, c.CreatedAt, 0
FROM dbo.Comments AS c
INNER JOIN dbo.ActionItems AS a ON a.Id = c.RelatedEntityId
WHERE c.RelatedEntityType = N'ActionItem'
  AND NOT EXISTS (SELECT 1 FROM dbo.ActionItemComments AS x WHERE x.Id = c.Id);
-- </copy>

PRINT CONCAT('Copied ', @@ROWCOUNT, ' action-item comment(s) into dbo.ActionItemComments.');

COMMIT TRANSACTION;
