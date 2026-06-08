# HRIS Leave Application — End-to-End Test Plan

Workflow under test: **Apply → Approve/Reject → Deduction → Balance**.
Covers every behavior changed during the recent rework (Life Class 1.5×, LWP year-reset, priority cascade, Admin Saturday weekday rule, same-day duplicate prevention, holiday/Sunday handling, accurate result messages, HR-direct-add routing, authorization, and the apply-time mix-rule for Life Class).

All tests are written so you can run them via Swagger / Postman against a local DB. A SQL "expected ledger" snippet is included where it helps confirm what the engine actually wrote.

---

## 0. Prerequisites (do these once)

### 0.1 Deploy migrations & SPs (order matters)

1. `Migrations/2026-06-06_HRISLeaveMaster_DeductionPriority_int_and_ApplicableOnWeekDay.sql` — `DeductionPriority bit→int` + add `ApplicableOnWeekDay`.
2. `Migrations/2026-06-06_HRISLeaveApplication_IsLifeClass.sql` — add `IsLifeClass` to leave applications.
3. `Table/HRTeam.sql` — drop the obsolete `HRTeam` table (no-op if it never existed).
4. Deploy SPs:
   - `SP_GetStaffAuthContextByLogin` (HR via `StaffRoleBaseRightMaster`)
   - `SP_UpsertHRISLeaveApplication`
   - `SP_ApproveRejectHRISLeaveApplication`
   - `SP_GetLeaveSummaryByStaffID`
   - `SP_GetHRISLeaveApplication`
   - `SP_GetHRISLeaveApplicationsForApproval`
   - `SP_InsertUpdateHRISLeaveMaster`, `SP_GetAllHRISLeaveMaster`, `SP_GetHRISLeaveMasterByID`
5. Rebuild backend (model + controller changes).
6. Rebuild frontend (`isHR: false` default).

### 0.2 Seed test data

| Step | Data |
|---|---|
| Leave master priorities | `UPDATE HRISLeaveMaster SET DeductionPriority = 1 WHERE LeaveName='Privileged Leave'`<br>`SET DeductionPriority = 2 WHERE LeaveName='Casual Leave'`<br>`SET DeductionPriority = 3 WHERE LeaveName='Sick Leave'`<br>`SET DeductionPriority = NULL WHERE IsLWP = 1 OR LeaveName LIKE '%Admin Saturday%'` |
| Admin Saturday | `UPDATE HRISLeaveMaster SET ApplicableOnWeekDay = 6 WHERE LeaveName LIKE '%Admin Saturday%'` |
| Allocations for `staffA` (your applicant) | PL = 5, CL = 5, SL = 5, LWP = 200, AdminSat = 4 (in `HRISLeaveDetails`) |
| `staffA.ReportingToID` | `staffB` (manager) |
| HR member | Add `staffH` via Management & Coordination Team UI → Position: `HR Team` |
| Life Class day | Add an `HRISStaffCalendar` row: `IsLifeClass = 1`, `FromDate = ToDate = <test date>`, link to `staffA` via `HRISStaffCalendarStaffList` (or use a Whole School entry) |
| Holiday | Add a `HolidayList` row inside one of the multi-day test ranges |

### 0.3 Cast of characters

| Identifier | Role | Notes |
|---|---|---|
| `staffA` | Applicant | Logs in with email A |
| `staffB` | Reporting Manager of `staffA` | Logs in with email B |
| `staffH` | HR Team member | Logs in with email H |
| `staffX` | Unrelated staff | Logs in with email X — used to test the auth gate |

---

## 1. Apply Flow — happy path

### TC-1.1 Apply for a single working day
**Pre:** No prior application overlapping the date. `staffA` logged in.
**Endpoint:** `POST /api/HRISLeaveApplication/UpsertLeaveApplication`
**Body:**
```json
{
  "leaveApplicationID": 0,
  "forStaffID": <staffA>,
  "leaveMasterID": <CL>,
  "fromDate": "2026-06-10",
  "toDate": "2026-06-10",
  "reason": "Personal",
  "isPriorInformedToManager": true,
  "isHR": false,
  "createdBy": <staffA>
}
```
**Expected:**
- `IsSuccess = true`, message `"Leave application submitted successfully."`
- Row in `HRISLeaveApplication`: `StatusID = 1` (Pending), `ReportingTo = staffB`, `IsLifeClass = 0`, `TotalDays = 1`.
- **No** rows yet in `HRIS_LeaveDeductionRecords`.

---

### TC-1.2 Apply for a multi-day range (the original "01-Jun → 03-Jun" bug)
**Pre:** Clean slate for that range.
**Body:** `fromDate = 2026-06-15`, `toDate = 2026-06-17`, `leaveMasterID = <SL>`.
**Expected:**
- Application created Pending, `TotalDays = 3` (assuming no Sunday/holiday inside).
- After manager approval (TC-4.1), **3 rows** in `HRIS_LeaveDeductionRecords` — one per working day, each with `LeaveDeductionValue = 1.0`.

**Verification:**
```sql
SELECT InstanceDate, LeaveMasterID, LeaveDeductionValue, DeductionValue, IsTLApproved
FROM HRIS_LeaveDeductionRecords
WHERE StaffID = <staffA> AND InstanceDate BETWEEN '2026-06-15' AND '2026-06-17';
```

---

### TC-1.3 Apply for a range spanning Sunday and/or holiday
**Pre:** Add `HolidayList` row for `2026-06-25` (Thursday) for the test board. The range `2026-06-22 (Mon) → 2026-06-28 (Sun)` includes a holiday and a Sunday.
**Expected at apply:** Accepted; `TotalDays = 5` (Mon, Tue, Wed, Fri, Sat — Thu holiday + Sun excluded).
**Expected at approval:** 5 deduction rows; **no row for Thu or Sun**.
**Message at apply:** `"Leave application submitted successfully."`

---

### TC-1.4 Apply range with no working days (all Sundays/holidays)
**Pre:** Range = a single Sunday, e.g. `2026-06-21 → 2026-06-21`.
**Expected:** Apply rejected with `"The selected date range has no working days to apply leave for (only Sundays/holidays)."`

---

## 2. Same-day duplicate prevention

### TC-2.1 Apply a second leave that overlaps an existing pending/approved one
**Pre:** TC-1.1 already created an application for `2026-06-10`.
**Body:** new application for `2026-06-08 → 2026-06-12`.
**Expected:** Apply rejected with `"A leave application already exists for one or more of the selected dates. You cannot re-apply for the same date(s)."`

### TC-2.2 Editing the same application (no false positive)
**Pre:** Application id `N` exists for `2026-06-10`.
**Body:** Upsert with `leaveApplicationID = N`, dates unchanged.
**Expected:** Allowed (the overlap check ignores the row being edited).

---

## 3. Life Class behavior (apply-time flag, faithful to legacy)

### TC-3.1 Apply on a Life Class day — single day
**Pre:** `2026-06-13` is a Life Class day for `staffA`.
**Expected:**
- Application created with `IsLifeClass = 1`.
- After approval: one deduction row, `LeaveDeductionValue = 1.5`, reason prefixed `"Life class - "`, remark `"1 deduction - 1.5 (Life class)"`.

### TC-3.2 Apply Life Class day with LWP leave type
**Pre:** Same day as above, but `leaveMasterID = <LWP>`.
**Expected:** Flat LWP deduction = **1.5** (slab calc skipped), remark `"1 deduction - 1.5 (Life class)"`.

### TC-3.3 Mix rule — Life Class + regular day in the same application
**Pre:** Range = `2026-06-13 (Life Class)` to `2026-06-15` (no Life Class).
**Expected:** Apply rejected at apply time with `"You cannot apply Life Class days and regular day leave together. Please apply them separately."`

### TC-3.4 Approval reads the persisted flag (not a fresh calendar query)
**Steps:**
1. Apply on a Life Class day → `IsLifeClass = 1` saved.
2. Before approval, an admin **deletes** the Life Class calendar row.
3. Approve.
**Expected:** Deduction is still **1.5** (flag at apply time wins, matching legacy semantics).

### TC-3.5 No Life Class days at all
**Expected:** `IsLifeClass = 0` on the row; deduction = 1.0 per working day.

---

## 4. Manager approval (basic deduction)

### TC-4.1 Manager approves a multi-day paid leave
**Pre:** Application from TC-1.2 (3 SL days, Pending). `staffB` (manager) logged in.
**Endpoint:** `POST /api/HRISLeaveApplication/ApproveRejectLeaveApplication`
**Body:**
```json
{ "leaveApplicationID": <N>, "statusID": 2, "approverComment": "Approved by TL" }
```
*(Note: `approverStaffID` is **ignored** even if you send it — the controller derives it from the token.)*
**Expected:**
- Response: `"Leave application approved and deduction(s) generated successfully."`
- 3 rows in `HRIS_LeaveDeductionRecords` for those dates, `LeaveMasterID = <SL>`, `LeaveDeductionValue = 1.0`, `ApproverID = staffB`, `IsTLApproved = 1`.

### TC-4.2 Approving an already-approved application
**Expected:** `"Leave application already processed."` `IsSuccess = true` (idempotent).

### TC-4.3 Re-approve an already-rejected (or vice-versa)
**Expected:** `"Leave application has already been processed and cannot be modified."` `IsSuccess = false`.

---

## 5. Priority cascade (when chosen type runs short)

### TC-5.1 Cascade across types as quota exhausts
**Pre-state per the user's example:**
- Allocations: PL 5, CL 5, SL 5, LWP 200.
- Already deducted **before** this test (use prior approvals to set up):
  - PL: 5 used (exhausted)
  - CL: 4 used (1 remaining)
  - SL: 3 used (2 remaining)
  - LWP: 10 used
- Priorities: PL=1, CL=2, SL=3.

**Action:** `staffA` applies 2 CL days `2026-07-06 → 2026-07-07` (Mon–Tue). Manager approves.
**Expected deduction rows:**
| InstanceDate | LeaveMasterID | LeaveDeductionValue | Reason / Remark |
|---|---|---|---|
| 2026-07-06 | CL | 1.0 | from chosen type (last 1 left) |
| 2026-07-07 | SL | 1.0 | cascaded — CL exhausted, SL has balance |

**Then** apply **2 more SL days** `2026-07-08 → 2026-07-09`:
| Date | LeaveMasterID | LeaveDeductionValue | Notes |
|---|---|---|---|
| 2026-07-08 | SL | 1.0 | last SL |
| 2026-07-09 | LWP | 1.0 | all paid exhausted → LWP via slab |

**SQL check:**
```sql
SELECT InstanceDate, LeaveMasterID, LeaveDeductionValue, LWPDeductionRemark
FROM HRIS_LeaveDeductionRecords
WHERE StaffID=<staffA> AND InstanceDate BETWEEN '2026-07-06' AND '2026-07-09'
ORDER BY InstanceDate;
```

### TC-5.2 Apply gate does NOT block over-application
**Action:** With CL fully exhausted, apply 2 CL days.
**Expected at apply:** Accepted (no quota error). Cascade absorbs at approval time.

### TC-5.3 LWP applications bypass the apply-time quota gate
**Action:** Apply 5 LWP days even though `HRISLeaveDetails.NoOfLeave = 0` for LWP.
**Expected:** Accepted at apply (LWP is unlimited by definition).

---

## 6. Admin Saturday (configurable weekday rule)

### TC-6.1 Admin Saturday applied on a Saturday
**Pre:** `2026-07-04` is a Saturday. `HRISLeaveMaster.ApplicableOnWeekDay = 6` for Admin Saturday.
**Action:** Apply `leaveMasterID = <AdminSaturday>`, `fromDate = toDate = 2026-07-04`.
**Expected:** Accepted; on approval, deducts 1 Admin Saturday.

### TC-6.2 Admin Saturday applied on a non-Saturday
**Action:** Same as above but `2026-07-06` (Monday).
**Expected:** Rejected at apply with `"This leave can only be applied on a Saturday."`

### TC-6.3 Admin Saturday quota cascade isolation
**Pre:** `Admin Saturday.DeductionPriority = NULL` (not in cascade).
**Action:** Exhaust Admin Saturday quota (4 used). Apply a 5th Saturday → approve.
**Expected:** Goes straight to LWP (does not cascade into PL/CL/SL because AdminSat is not part of the priority list).

---

## 7. Rejection scenarios

### TC-7.1 Reject a **past** leave
**Pre:** Application for `2026-05-20 → 2026-05-22` (entirely in the past), Pending.
**Action:** Reject.
**Expected:**
- Message: `"Leave application rejected and LWP deduction(s) applied."`
- 3 LWP deduction rows; `LWPDeductionRemark` begins with `"Previously applied as <OriginalLeaveType> and was rejected, hence LWP deduction is applied (...)"`.
- `HRISLeaveApplication.LeaveMasterID` updated to the LWP master id.

### TC-7.2 Reject a **future** leave
**Pre:** Application for `2026-12-15 → 2026-12-17` (entirely in the future), Pending.
**Action:** Reject.
**Expected:**
- Message: `"Rejected future <OriginalLeaveType> request. No deduction generated."`
- **Zero** rows in `HRIS_LeaveDeductionRecords` for those dates.

### TC-7.3 Reject a leave straddling today
**Pre:** `fromDate < today < toDate` (e.g. yesterday → tomorrow), Pending.
**Expected:** Past + today days get LWP deduction rows; future days get **no** rows.

---

## 8. Authorization — who can approve/reject

### TC-8.1 Reporting manager approves their report
**Caller:** `staffB` (token = email B).
**Expected:** Allowed. `ApproverID = staffB` on the deduction rows.

### TC-8.2 HR Team member approves any leave
**Caller:** `staffH` (in `StaffRoleBaseRightMaster` with Position = `'HR Team'`).
**Expected:** Allowed for any application. `ApproverID = staffH`.

### TC-8.3 Unrelated staff (not manager, not HR) tries to approve
**Caller:** `staffX`.
**Expected:** `IsSuccess = false`, message `"You are not authorized to approve or reject this leave application. Only the reporting manager or an HR Team member can approve/reject."`

### TC-8.4 Client tries to spoof `approverStaffID`
**Body:** `"approverStaffID": <staffB>` while logged in as `staffX`.
**Expected:** Still rejected as in TC-8.3. The controller overrides `approverStaffID` with the logged-in user's StaffID before the SP runs.

### TC-8.5 Approval queue scoping
| Caller | Endpoint result |
|---|---|
| `staffB` (manager only) | Only applications where `ReportingTo = staffB` |
| `staffH` (HR Team) | All applications |
| `staffX` (neither) | Empty list (no applications match `ReportingTo = staffX`) |

`GET /api/HRISLeaveApplication/GetLeaveApplicationsForApproval?AcademicYearID=<id>`

---

## 9. `isHR` flag — no more auto-approval for everyone

### TC-9.1 Normal staff apply (not HR) — no auto-approve
**Caller:** `staffA` (not in HR Team).
**Expected:** `StatusID = 1` (Pending). Message: `"Leave application submitted successfully."` (Not "submitted and approved".) No deduction rows generated.

### TC-9.2 HR member applies for themselves
**Caller:** `staffH` (HR Team), `forStaffID = staffH`.
**Expected:** Treated as a normal application (Pending). The server sets `IsHR = false` because applicant == caller.

### TC-9.3 HR member applies on behalf of someone else (HR-direct-add)
**Caller:** `staffH`, `forStaffID = staffA`.
**Expected:**
- Server sets `IsHR = true`.
- Application is inserted Pending, then immediately routed through `SP_ApproveRejectHRISLeaveApplication`.
- Final state: `StatusID = 2` (Approved); deduction rows generated; message `"Leave application submitted and approved successfully."`
- `ApproverID = staffH` on the deductions.

---

## 10. Balance & summary consistency

After running TC-3.1 (one Life Class day, approved):
```sql
EXEC SP_GetLeaveSummaryByStaffID @StaffID=<staffA>, @AcademicYearID=<id>;
EXEC SP_GetHRISLeaveApplication  @StaffID=<staffA>, @AcademicYearID=<id>;
```
**Expected — both result sets agree:**
- For the chosen paid type: `availed` includes `1.5`, not `1.0`.
- LWP `availed` reflects flat 1.5 if it was an LWP Life Class day.

### TC-10.1 Future-dated approved leave shows as `preApproved`, not `availed`
**Pre:** Approve a leave whose dates are entirely in the future.
**Expected:** Summary `availed = 0` for those dates; `preApproved = totalDays`.

---

## 11. LWP slab + academic-year reset

### TC-11.1 LWP slab progresses across many days within a year
**Pre:** `HRISLWPDeductionPolicyDetails` rows: `0–3 @ 1.0`, `3–10 @ 0.5`.
**Action:** Apply + approve 5 LWP days for `staffA` in `AY 2026-27`.
**Expected per day:**
| Day | UsedLWP before | DeductionValue (salary) | LWPDeductionRemark |
|---|---|---|---|
| 1 | 0 | 1.0 | `1 deduction - 1.0` |
| 2 | 1 | 1.0 | `1 deduction - 1.0` |
| 3 | 2 | 1.0 | `1 deduction - 1.0` |
| 4 | 3 | crosses → `0.0 × 1.0 + 1.0 × 0.5 = 0.5` | `0 deduction - 0 and 1 deduction - 0.5` |
| 5 | 4 | 0.5 | `1 deduction - 0.5` |

### TC-11.2 Slab resets per academic year
**Action:** Move clock to a date in `AY 2027-28` and apply LWP again.
**Expected:** First-day deduction uses the **first slab** (`0–3 @ 1.0`), not the prior year's running total.

**SQL check** — `@UsedLWP` is scoped to AY:
```sql
SELECT AcademicYearID, SUM(LeaveDeductionValue)
FROM HRIS_LeaveDeductionRecords r
JOIN AcademicYear ay ON r.InstanceDate BETWEEN ay.FromDate AND ay.ToDate
WHERE r.StaffID = <staffA> AND r.LeaveMasterID = <LWP>
GROUP BY AcademicYearID;
```

---

## 12. Result-message accuracy (a regression we explicitly fixed)

| Scenario | Expected message |
|---|---|
| Approve a normal paid leave | `Leave application approved and deduction(s) generated successfully.` |
| Approve a leave whose range has zero working days (all Sunday/holiday) — should not happen because apply blocks it, but if it does | `Leave application approved. No deduction generated (the range had no deductible working days).` |
| Reject a past leave | `Leave application rejected and LWP deduction(s) applied.` |
| Reject a future leave | `Rejected future <LeaveType> request. No deduction generated.` |
| Approve/Reject when already in that state | `Leave application already processed.` |
| Try to flip Approved↔Rejected | `Leave application has already been processed and cannot be modified.` |
| Unauthorized caller | `You are not authorized to approve or reject this leave application. Only the reporting manager or an HR Team member can approve/reject.` |

---

## 13. End-to-end golden path (a single scripted run)

Run this script top-to-bottom to exercise the major paths in one go.

| Step | Action | Caller | Expectation |
|---|---|---|---|
| 1 | Apply CL `2026-08-03 → 2026-08-05` (3 working days) | `staffA` | Pending; `IsLifeClass = 0` |
| 2 | `staffX` tries to approve | `staffX` | 403-equivalent ("not authorized") |
| 3 | Manager approves | `staffB` | 3 CL deduction rows of 1.0 |
| 4 | Apply on a Life Class day `2026-08-07` | `staffA` | Pending; `IsLifeClass = 1` |
| 5 | HR approves | `staffH` | 1 row at 1.5 |
| 6 | Apply Admin Saturday on Tuesday | `staffA` | Apply rejected (`"only on a Saturday"`) |
| 7 | Apply Admin Saturday on Saturday `2026-08-08` | `staffA` | Accepted |
| 8 | Apply CL again such that CL has 0 balance | `staffA` | Accepted (no gate) |
| 9 | Manager approves the over-applied CL | `staffB` | Cascade: spills into next paid type, then LWP if needed |
| 10 | Apply future CL `2027-01-10 → 2027-01-12` | `staffA` | Pending |
| 11 | Manager rejects the future leave | `staffB` | Message contains `"No deduction generated"`; no rows written |
| 12 | Apply past CL `2026-05-04 → 2026-05-06`, manager rejects | `staffB` | 3 LWP rows with `"Previously applied as Casual Leave and was rejected…"` remarks; application's `LeaveMasterID` flipped to LWP |
| 13 | Apply overlapping range | `staffA` | Rejected (`"A leave application already exists…"`) |
| 14 | HR adds leave for `staffA` from HR-add path | `staffH` | Final status Approved + deduction rows in one call |

If all 14 pass, the workflow is end-to-end healthy.

---

## 14. Quick verification queries

```sql
-- Authorization context resolution
EXEC SP_GetStaffAuthContextByLogin @LoginName = '<staffH email or mobile>';

-- Latest applications + status
SELECT TOP 20 LeaveApplicationID, ForStaffID, ReportingTo, LeaveMasterID, StatusID,
              IsLifeClass, TotalDays, FromDate, ToDate, CreatedOn
FROM HRISLeaveApplication ORDER BY CreatedOn DESC;

-- Deduction ledger for a staff
SELECT InstanceDate, LeaveMasterID, LeaveDeductionValue, DeductionValue,
       ApproverID, IsTLApproved, IsTLRejected, LWPDeductionRemark, Reason
FROM HRIS_LeaveDeductionRecords
WHERE StaffID = <staffA>
ORDER BY InstanceDate;

-- HR Team membership
SELECT StaffID, Position, IsDelete
FROM StaffRoleBaseRightMaster
WHERE LTRIM(RTRIM(Position)) = 'HR Team' AND IsDelete IS NULL;
```

---

## 15. Known limitations to be aware of (so you don't file them as bugs)

- **Username/password logins** (against `UserMaster`, not `Staff.SchoolEmailID`/`MobileNumber`) don't resolve to a `StaffID` server-side. They can apply, but cannot be identified as manager/HR → approve/reject is blocked for them.
- **Manager identification** depends on `Staff.ReportingToID` being correct. If a staff's manager is missing or wrong, only HR can approve.
- **No half-day support** (full-day only, per senior's decision).
- **No leave reversal feature** yet — once deductions are written, there's no built-in undo.
- **`HRIS_LeaveDeductionRecords` has no `LeaveApplicationID` column**, so deletions of applications don't cascade to the ledger (deliberately deferred).
- **Holiday/board scoping**: `HolidayList` lookups are not yet scoped by board/campus/academic-year (legacy gap, on the backlog).
