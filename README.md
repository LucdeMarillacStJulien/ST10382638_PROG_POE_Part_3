# Lecturer Claims Management System — Part 2 (PROG6212)

##  Overview
This repository contains **Part 2** of the Lecturer Claims Management System.  
Part 2 focuses on UI/UX polish, validation, secure file handling (encryption/decryption), approval workflow, error handling, and unit testing.

---
##  YouTube Demo

- [ST10382638 PROG6212 POE PART 2](https://youtu.be/kdibIv_qLhs)
---

## Changes from Part 1 → Part 2
- Added **supporting document uploads** linked to each claim.  
- Implemented **AES encryption/decryption** for stored documents.  
- Expanded **approval workflow**: Programme Coordinator (Verify/Reject) and Academic Manager (Approve/Reject).  
- Added **lecturer claim tracking** (Pending → Verified → Approved/Rejected).  
- Strengthened **validation** (attributes + server logic) and **error handling** with user-friendly messages.  
- Introduced **MSTest** unit tests for core flows.  
- UI/UX refinements for lecturer + admin views.  
- Regular, descriptive **Git commits** throughout development.  

---
## Instruction to Install and Run
1. Click on the Green code button with the text `<> Code`
2. Select the option download zip
3. Download the zip to a safe location
4. Unzip the file a place in the same or different folder as the zipped folder that you just downloaded.
5. Open the file and click on the sln file name ST10382638_PROG_POE
6. Click on the tools in the top bar of Visual Studio
7. Then hover over Nuget Package Manager
8. You will see the options pop up alongside, Select Package Manager Console
9. Run the following commands these commands can be copied and pasted
- `Add-Migration LecturerMigration`
- `Update-Database`
10. Once both commands have been run you can run the program by clicking the green arrow at the top of Visual Studio.
---

##  Lecturer Features
- Submit claims with **Hours Worked**, **Hourly Rate (at submission)**, and **Notes**.  
- Upload supporting documents restricted to `.pdf / .docx / .xlsx`.  
- Selected filenames are displayed immediately on the claim form and remain visible with the claim record after submission.  
- Track claim status updates as claims move through approvals.  

---

##  Admin Features
**Programme Coordinator**  
- View all pending claims (with document access).  
- Verify or Reject claims.  

**Academic Manager**  
- View all verified claims (with document access).  
- Approve or Reject claims.  

---

##  File Handling & Security
- **Allowed file types**: `.pdf`, `.docx`, `.xlsx`  
- **Maximum file size**: **10 MB per file** (enforced server-side)  
- Files are **encrypted at rest**. Downloads are provided via an on-the-fly decrypted ZIP for each claim.  

---

##  Error Handling & Validation
- **Hours Worked** validation:  
  - Minimum allowed: **0.25 hours** per claim  
  - Maximum allowed: **10.0 hours** per claim  
  - Errors shown if values are below/above limits or invalid.  
- **File uploads**:  
  - Rejects invalid types with a clear error message.  
  - Rejects files larger than **10 MB** with a clear message.  
- Graceful handling of encryption/decryption and file I/O exceptions.  
- Errors are returned via **ModelState** with user-friendly text.  

---

##  Unit Testing (MSTest)
This project includes **12 unit tests**, covering validation, encryption, error handling, and workflow transitions.  

###  ClaimControllerTests.cs
- `Create_InvalidFileType_AddsModelErrorAndReturnsView()`  
- `Create_ValidSubmission_SavesClaimAndEncryptedDocs()`  
- `Approve_SetsStatusVerified()`  
- `Reject_SetsStatusRejected()`  
- `ManagerApprove_OnlyAllowsApprovedWhenVerified()`  
- `Create_FileOver10MB_AddsModelErrorAndReturnsView()`  
- `Create_InvalidHours_NonPositive_ReturnsViewWithError()`  
- `Create_InvalidHours_Over200_ReturnsViewWithError()`  

###  ClaimDownloadTests.cs
- `BuildDecryptedZipAsync_ReturnsZipWithFilesAtRoot()`  

###  EncryptionTests.cs
- `EncryptDecrypt_RoundTrip_Works()`  
- `Encryption_Throws_WhenKeyMissing()`  

These tests demonstrate thorough coverage of **file validation, claim rules, encryption, and approval logic**.

---

##  SQL Schema (Reference)
```sql
## SQL Schema

CREATE TABLE dbo.[User] (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    FirstName    NVARCHAR(MAX) NOT NULL,
    Surname      NVARCHAR(MAX) NOT NULL,
    PhoneNumber  NVARCHAR(MAX) NOT NULL,
    Email        NVARCHAR(MAX) NOT NULL,
    Role         NVARCHAR(MAX) NOT NULL
);

CREATE TABLE dbo.LecturerProfile (
    LecturerProfileId INT IDENTITY(1,1) PRIMARY KEY,
    HourlyRate        FLOAT        NOT NULL, -- matches C# double
    IsAvailable       BIT          NOT NULL, -- matches C# bool
    UserId            INT          NOT NULL
        CONSTRAINT FK_LecturerProfile_User REFERENCES dbo.[User](UserId)
        ON DELETE CASCADE
);

CREATE TABLE dbo.Claim (
    ClaimId           INT IDENTITY(1,1) PRIMARY KEY,
    LecturerProfileId INT      NOT NULL
        CONSTRAINT FK_Claim_LecturerProfile REFERENCES dbo.LecturerProfile(LecturerProfileId)
        ON DELETE CASCADE,

    HoursWorked       FLOAT    NOT NULL,  -- matches double
    RateAtSubmission  FLOAT    NOT NULL,  -- matches double
    CalculatedAmount  FLOAT    NOT NULL,  -- matches double
    Notes             NVARCHAR(MAX) NULL,
    Status            NVARCHAR(MAX) NOT NULL,
    SubmittedOn       DATETIME2 NOT NULL
);

CREATE TABLE dbo.SupportingDoc (
    DocumentId  INT IDENTITY(1,1) PRIMARY KEY,
    ClaimId     INT NOT NULL
        CONSTRAINT FK_SupportingDoc_Claim REFERENCES dbo.Claim(ClaimId)
        ON DELETE CASCADE,

    FileName    NVARCHAR(MAX) NOT NULL,
    FileUrl     NVARCHAR(MAX) NOT NULL,
    FileType    NVARCHAR(MAX) NOT NULL
);

```
