==================================================
BATCH 4 — ACADEMIC YEAR, TERM, ENROLLMENT AND SECTIONS
======================================================

Continue using the original TechWise360 master requirements, existing codebase, all completed batches, and the current master implementation checklist.

Proceed to:

# Batch 4 — Academic Year, Term, Enrollment, Student Data, and Section Management

Do NOT restart previously completed work.

The primary objective is to fix the school-year data architecture identified by the adviser.

---

## 1. ACADEMIC YEAR

Teachers/admins must be able to create new academic years.

Examples:

* 2026–2027
* 2027–2028
* 2028–2029

DO NOT overwrite previous academic years.

Historical data must remain retrievable.

Implement appropriate:

* IDs
* labels
* start/end year/date
* active/inactive status
* timestamps

Only one academic year should normally be active unless the existing architecture intentionally supports otherwise.

Changing the active academic year must not delete old records.

---

## 2. HISTORICAL RECORDS

Verify that historical:

* enrollments
* assessments
* grades
* class records
* VR results
* terms
* reports

remain associated with their original academic year.

Do not overwrite relationships when a new year becomes active.

---

## 3. TERM MANAGEMENT

Terms must belong to an Academic Year.

Do not use one permanent global First/Second/Third Term record that gets overwritten.

Example:

Academic Year 2026–2027
→ First Term
→ Second Term
→ Third Term

Academic Year 2027–2028
→ First Term
→ Second Term
→ Third Term

Allow creation/activation where appropriate.

Historical terms must remain accessible.

---

## 4. REGISTRATION / ENROLLMENT

Fix enrollment so each enrollment clearly belongs to:

* student
* academic year
* term
* grade level
* section

Do not rely on invisible assumptions.

If the current UI uses active Academic Year and Term, clearly display the current selection/context.

Prevent overwriting a student's previous enrollment record.

Prefer a proper Enrollment entity/relationship instead of duplicating student accounts.

---

## 5. ACTIVE ACADEMIC YEAR AND TERM

Provide a clear, reliable way to determine:

* current active academic year
* active term

Ensure enrollment, class records, assessments, and relevant teacher screens use correct context.

Do not make historical data disappear merely because another year is active.

---

## 6. FULL NAME REDUNDANCY

Inspect why the registration form contains:

* First Name
* Last Name
* Full Name

If Full Name has no separate legitimate purpose:

remove the manually editable Full Name field.

Generate the display name from the actual name fields.

If a derived fullName is required internally for search/indexing, generate it automatically.

Do not make students/admins enter the same data twice.

---

## 7. STUDENT FIELDS

Review registration fields.

Grade level should support the project scope:

* Grade 9
* Grade 10

unless the project already intentionally supports configurable grade levels.

Review Hometown.

Retain it if it has a real purpose, such as students outside Camantiles.

Avoid unnecessary/reduntant fields.

---

## 8. SECTION MANAGEMENT

Do not hard-code only three sections forever.

Implement flexible sections.

Preferred approach:

* proper Section entity
* add section
* rename if allowed
* activate/deactivate if appropriate
* associate sections with grade/year if architecture supports it

If adviser specifically requires "Others":

Provide an Others/Add New Section path that creates a real section and then makes it selectable.

Avoid repeatedly storing arbitrary free-text section names if a relational model is appropriate.

---

## 9. DATA VALIDATION

Handle:

* duplicate academic year
* invalid year/date ranges
* duplicate term
* duplicate enrollment
* invalid active academic year
* invalid active term
* missing section
* wrong grade
* invalid student/year relationship

Use understandable messages.

---

## 10. DATABASE MIGRATIONS

Use proper migrations.

Do NOT:

* wipe production data
* reset the database
* drop historical data
* replace existing records carelessly

Migrate legacy records safely.

---

## 11. UI

Update relevant interfaces professionally:

* Academic Year management
* Term management
* Registration/Enrollment
* Section management
* filters

Keep the existing TechWise360 design system.

Do not perform unrelated visual redesign.

---

## 12. VR RELATIONSHIPS

Verify that Batch 3 VR results correctly reference:

* Academic Year
* Term
* Enrollment context

If Batch 4 schema improvements require targeted fixes to Batch 3, update them without breaking result integration.

---

# BATCH 4 ACCEPTANCE TEST

Test:

1. Create Academic Year 2027–2028.
2. Previous year remains available.
3. Activate new academic year.
4. Create/select its terms.
5. Enroll a student into Grade 9 + Section + Term.
6. Student's older enrollment remains intact.
7. Add a new section.
8. New section becomes selectable.
9. Historical class records still load.
10. Historical VR results still belong to the old year.
11. New VR results can belong to the new year/term.

---

# BATCH 4 COMPLETION REPORT

Provide:

* database changes
* migrations
* academic year implementation
* term implementation
* enrollment implementation
* section implementation
* redundant fields removed/fixed
* historical-data preservation verification
* tests performed
* remaining issues
* updated master checklist

Do NOT proceed to Batch 5 automatically.
