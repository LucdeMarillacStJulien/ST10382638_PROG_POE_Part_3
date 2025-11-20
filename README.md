# Contract Monthly Claim System (CMCS) — Part 3 Automation  
PROG6212 • Programming 2B  
Student: Luc de Marillac St Julien (ST10382638)  

---

## 1. Overview

This repository contains **Part 3** of the Contract Monthly Claim System (CMCS) POE.  

The system is a **.NET 8 ASP.NET Core MVC** web application that streamlines monthly claim submission and approval for Independent Contractor lecturers:

- Lecturers submit claims with hours worked and supporting documents.
- Programme Coordinators and Academic Managers verify and approve claims.
- HR acts as a “super user” who manages users and can generate lecturer invoices/reports.
- Claim status can be tracked end-to-end, with a focus on automation and validation.

Technology stack (as implemented in this solution):

- **ASP.NET Core 8 MVC** (`net8.0`)
- **Entity Framework Core** (SQL Server)
- **ASP.NET Core Identity** for authentication and role-based authorisation
- **Bootstrap 5, custom CSS (`ui.css`) and JavaScript** for UI and automation
- **MSTest** test project for unit tests
- AES-based encryption service and download service for secure document handling

---

## 2. Roles and High-Level Features

### Lecturer

- Log in with credentials created by HR.
- View their own lecturer profile (name, surname, hourly rate).
- **Submit claims**:
  - `HoursWorked` only (hourly rate is pulled from the lecturer profile).
  - Notes / comments.
  - Multiple supporting documents (`.pdf`, `.docx`, `.xlsx`).
- **Auto-calculation**:
  - Client-side JavaScript (`wwwroot/js/claim-automation.js`) calculates `CalculatedAmount = HoursWorked × HourlyRate`.
  - Server-side code re-calculates and validates the amount before saving.
- **Validation**:
  - Hours per claim limited to a safe range (e.g., 0.25–10 hours) with server-side checks.
  - Model validation errors shown inline on the claim submission form.
- **Tracking**:
  - View their own claims and statuses as they move through:
    - `Pending` → `Verified` (Coordinator) → `Approved` / `Rejected` (Manager).

### Programme Coordinator

- Secured access via Identity role.
- Dashboard listing **pending** claims for verification.
- View full claim details:
  - Lecturer information (from `LecturerProfile` + `ApplicationUser`).
  - Hours, rate, calculated amount, notes.
  - Encrypted supporting documents (downloadable via a dedicated download service).
- **Automation / validation support** via `ClaimEvaluationService`:
  - Checks hours range, positive rate, and amount math (Hours × Rate).
  - Generates a summary of any issues before verifying.
- Actions:
  - **Verify** valid claims.
  - **Reject** claims with reasons.

### Academic Manager

- Separate Manager dashboard focusing on claims already verified by the Coordinator.
- View verified claims with the same contextual information and documents.
- Actions:
  - **Approve** final payment.
  - **Reject** if issues remain.

### HR (Super User)

- HR role implemented with a dedicated `HRController` and views (`Index`, `Create`, `Edit`, `Details`).
- Responsibilities:
  - **Create all users** (no public registration).
    - Capture name, surname, email, role, and lecturer hourly rate where applicable.
  - **Update user information**:
    - Edit personal details and lecturer hourly rate.
    - Reset passwords where required.
  - **Generate reports / invoices**:
    - Uses `LecturerReportService` to build CSV “invoice” exports:
      - Per single claim (`BuildSingleClaimInvoiceAsync`).
      - Aggregated by **day/week/month** for a lecturer (`BuildPeriodInvoiceAsync`).
    - Downloaded as CSV files that can be opened in Excel / imported into finance systems.

---

## 3. Part 3 Automation Enhancements (from Part 2 → Part 3)

Part 2 already implemented:

- Lecturer claim submission with validation.
- Coordinator and Manager approval workflow.
- Secure supporting document upload and encryption.
- Status tracking and unit tests.

**Part 3 focuses on automation, HR super user, and refining validation and file handling.**

### 3.1 Lecturer View Automation

- **Hourly rate is no longer typed by the lecturer**:
  - Pulled directly from `LecturerProfile.HourlyRate` (maintained by HR) and rendered as a read-only field on the claim form.
- **Auto-calculation on the client**:
  - `claim-automation.js`:
    - Listens to changes on the `HoursWorked` input.
    - Reads the read-only `rateAtSubmission` value.
    - Calculates `CalculatedAmount` and updates:
      - An on-screen preview label.
      - A hidden `CalculatedAmount` field posted with the form.
- **Server-side re-calculation and validation**:
  - Claim controller verifies that `CalculatedAmount ≈ HoursWorked × RateAtSubmission`.
  - Rejects or flags claims where the amount does not match.
- **Hours validation**:
  - Guard against obviously invalid hours (e.g. negative, zero, or unrealistically large values per claim).
  - Validation errors are passed via `ModelState` and displayed on the claim form.

### 3.2 Coordinator and Manager Automation

- **ClaimEvaluationService** centralises automated checks:
  - Validates hours range, positive rate, and the correctness of the calculated amount.
  - Produces a summary string and a list of detected issues.
- The Coordinator and Manager views use these results to:
  - Quickly see if a claim passes automated checks.
  - Focus manual attention only on claims with issues.

### 3.3 HR View Automation

- **HR creates all accounts**:
  - HR creates `ApplicationUser` entries and links them to lecturer profiles with hourly rates.
  - This removes the need for public registration and aligns with the checklist.
- **Invoice/report generation**:
  - `LecturerReportService` builds CSV “invoice” files:
    - Per claim (single invoice with one line item).
    - By day/week/month (aggregating claims in a given period).
  - CSV includes:
    - Invoice number.
    - Lecturer details.
    - Claim IDs, hours, rate at submission, calculated amounts, status, notes.
- Download buttons are exposed on the HR lecturer detail view, making invoice generation a one-click operation.

---

## 4. Lecturer Feedback and How It Was Addressed (Part 2 → Part 3)

**Lecturer’s original feedback (Part 2):**

- Claim Submission feature: **20/20**
- Programme Coordinator & Manager views: **20/20**
- Document Upload feature: **18/20**
  - “Application crashes when trying to submit a claim with a large file (200MB).”
- Document Upload error handling: **9/10**
  - “Ensure that the application does not crash when uploading a large file (200MB), and that an appropriate error message is displayed to the user.”

**Part 3 changes to address this feedback:**

1. **Client-side file size guard**
   - New script: `wwwroot/js/claim-file-size-limit.js`.
   - The `<input id="Files" ... data-max-size="10485760" />` element in `Views/Claim/Create.cshtml` sets a **10 MB maximum** per file.
   - On `change`, the script:
     - Checks each selected file size against `data-max-size`.
     - Shows a clear `alert("...bigger than the maximum allowed size (10 MB).")`.
     - Clears the selection to prevent accidental submission.

2. **Server-side file size and type validation (defensive programming)**
   - Claim controller re-validates:
     - File extension: restricts to `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`.
     - File size: enforces the same 10 MB limit on the server.
   - If any file is invalid:
     - A `ModelState` error is added against `files`.
     - The view is redisplayed with a user-friendly error message.
   - This ensures the application **does not crash** even when the browser allows larger uploads.

3. **Consistent user feedback**
   - Validation messages are wired to `@Html.ValidationMessage("files", ...)` in the claim form.
   - Lecturers always see why an upload failed (type/size) instead of encountering a crash.

As a result, the large-file crash identified in Part 2 is handled gracefully in Part 3: oversized files are blocked with clear feedback and do not bring down the application.

---

## 5. Testing, Error Handling, and Reliability

A separate MSTest project (`Test.ST10382638_PROG_POE`) provides automated regression checks, including but not limited to:

- **EncryptionTests**:
  - Round-trip tests for the AES encryption/decryption service used for claim documents.
- **ClaimEvaluation tests**:
  - Ensure that hours, rate, and amount checks behave as expected for valid and invalid claims.
- **Controller-level tests** (where implemented):
  - Validate controller behaviour around creating claims, handling invalid input, and status transitions.

Error handling highlights:

- All critical controller actions wrap lookups and persistence in appropriate null checks and model validation.
- Invalid IDs and missing entities return `BadRequest` or `NotFound` rather than unhandled exceptions.
- File upload and claim validation guard against bad data to preserve stability.

---

## 6. How to Run the Application Locally

### 6.1 Prerequisites

- **Visual Studio 2022** (or later) with:
  - ASP.NET and web development workload.
  - .NET 8 SDK installed.
- **SQL Server LocalDB** (installed with Visual Studio).
- Internet access for NuGet package restore.

### 6.2 Clone / Download

1. On GitHub, click the green `<> Code` button.
2. Choose **Download ZIP** or **Clone** the repository.
3. If downloading ZIP:
   - Extract the contents to a folder (e.g. `C:\CMCS_Part3`).
4. Open `ST10382638_PROG_POE.sln` in Visual Studio.

### 6.3 Restore and Migrate Database

1. In Visual Studio, open **Tools → NuGet Package Manager → Package Manager Console**.
2. Ensure the **Default project** is `ST10382638_PROG_POE`.
3. Run (if migrations are not already applied):

   ```powershell
   Add-Migration InitialCreate
   Update-Database
   
### 6.4 Run the Application
1. Set ST10382638_PROG_POE as the startup project.
2. Press F5 (or click the green run button).
3. The site will launch in your browser at https://localhost:xxxx/.

Initial users and roles are seeded via the application’s seeding logic (see AppDbContext and startup configuration). HR logins can then be used to create additional lecturers, coordinators, and managers.

---

** Repository Contents (for ARC Submission)
This repository contains the following required artefacts for Part 3:
- ST10382638_PROG_POE/ — ASP.NET Core MVC solution and source code.
- Test.ST10382638_PROG_POE/ — MSTest unit test project.
- README.md — This documentation, updated for Part 3 automation and lecturer feedback.
- PROG6212_Part3_Presentation.pptx — PowerPoint presentation showcasing:
  - Updates from Part 2 → Part 3.
  - Key automated features and code screenshots.
- Final Part 3 YouTube demo link (unlisted) in the section below.
