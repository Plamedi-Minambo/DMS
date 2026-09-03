# Document Management System

## About This Project

I developed this Document Management System as part of a technical assessment. The main goal was to build a system that could manage financial documents such as invoices and credit notes, while also handling the approval process, duplicate checking, reporting, and AI-based insights.

I built the project as a full-stack application using **React for the frontend** and **C# ASP.NET Core Web API for the backend**, with **Entity Framework Core and SQLite** for the database.

While working on the project, I focused on making sure that the requirements from the assessment were not only implemented on the frontend, but also properly handled and protected on the backend.

---

# What I Built

The main features I implemented are:

* User login and authentication
* Role-based access
* Invoice and credit note uploads
* Automated invoice information extraction
* Duplicate invoice detection
* Three-stage approval workflow
* Document status tracking
* Reports and filtering
* PDF and Excel report exports
* AI Insights
* Backend API security

---

# 🔐 Login and User Roles

I implemented authentication using **ASP.NET Core Identity and JWT**.

There are five roles in the system:

* Admin
* Reviewer
* Manager
* Finance
* Viewer

Each role has different responsibilities. I wanted to make sure that a user could not simply access functions that they were not supposed to use.

## Test Accounts

I created the following accounts so that the project can be tested without having to create new users first.

> These accounts are for assessment/demo purposes only and should not be used as production credentials.

| Role         | Email               | Password           |
| ------------ | ------------------- | ------------------ |
| **Admin**    | `admin@test.com`    | `Admin@12345`      |
| **Reviewer** | `reviewer@test.com` | `N4@zT8!pQ6#Wm2$K` |
| **Manager**  | `manager1@test.com` | `M8#qT3@vN7!xR5$K` |
| **Finance**  | `finance1@test.com` | `F7@xR2!mQ9#kT4$L` |
| **Viewer**   | `viewer1@test.com`  | `V9!kQ4#tL7@xM2$R` |

---

# 👤 What Each Role Can Do

I set up the roles based on the responsibilities that would normally be associated with each part of the document approval process.

| Function                   | Admin | Reviewer | Manager | Finance | Viewer |
| -------------------------- | :---: | :------: | :-----: | :-----: | :----: |
| Login                      |   ✅   |     ✅    |    ✅    |    ✅    |    ✅   |
| Dashboard                  |   ✅   |     ✅    |    ✅    |    ✅    |    ✅   |
| View Documents             |   ✅   |     ✅    |    ✅    |    ✅    |    ✅   |
| Upload Documents           |   ✅   |     ✅    |    ✅    |    ✅    |    ❌   |
| View Reports               |   ✅   |     ✅    |    ✅    |    ✅    |    ✅   |
| Export Reports             |   ✅   |     ✅    |    ✅    |    ✅    |    ❌   |
| View AI Insights           |   ✅   |     ✅    |    ✅    |    ✅    |    ✅   |
| View Approval Workflow     |   ✅   |     ✅    |    ✅    |    ✅    |    ❌   |
| Reviewer Approval          |   ✅   |     ✅    |    ❌    |    ❌    |    ❌   |
| Manager Approval           |   ✅   |     ❌    |    ✅    |    ❌    |    ❌   |
| Finance Approval           |   ✅   |     ❌    |    ❌    |    ✅    |    ❌   |
| Reject at Authorised Stage |   ✅   |     ✅    |    ✅    |    ✅    |    ❌   |
| Create Users               |   ✅   |     ❌    |    ❌    |    ❌    |    ❌   |
| Assign Roles               |   ✅   |     ❌    |    ❌    |    ❌    |    ❌   |

### Admin

The Admin has access to the whole system. They can manage users, upload and view documents, access reports and AI Insights, and also perform any of the three approval stages.

### Reviewer

The Reviewer is responsible for the **first stage** of the approval process.

They can review a document and either approve or reject it at Stage 1, but they cannot perform the Manager or Finance approval stages.

### Manager

The Manager is responsible for **Stage 2**.

They can approve or reject a document once the Reviewer has approved it. They cannot perform the Reviewer or Finance approval stages.

### Finance

Finance is responsible for the **final approval stage**.

They can approve or reject a document at Stage 3, but they cannot approve the earlier Reviewer or Manager stages.

### Viewer

The Viewer has **read-only access**.

They can view the dashboard, documents, reports and AI Insights, but they cannot upload, approve, reject, or change documents.

---

# 📄 Document Upload

I created a dedicated Documents section where users can upload invoices and credit notes.

When a document is uploaded, the system:

1. Checks that the file is valid.
2. Stores the file.
3. Generates a unique stored filename.
4. Creates a SHA-256 hash for the file.
5. Saves the document information in the database.
6. Creates the three approval stages.
7. Attempts to extract the invoice information from the PDF.

This means that uploading a document automatically starts the rest of the process.

---

# 🤖 Invoice Information Extraction

One of the requirements was to automatically read information from the uploaded documents.

I implemented PDF text extraction to find information such as:

* Vendor
* Invoice number
* Invoice date
* Amount
* VAT
* Total amount
* Document type

I store this information separately in the database so that it can later be used by the approval, reporting and AI Insights sections.

### Current limitation

The current version works with **text-based PDFs**.

If a PDF is a scanned image with no selectable text, a proper OCR service would need to be added. I have identified this as one of the areas I would improve in a production version.

---

# 🔎 Duplicate Detection

I implemented duplicate checking in a few different ways.

### Invoice Number

The system checks if the invoice number already exists.

If it does, the system identifies it as a potential duplicate.

### Vendor + Amount

I also added a secondary check using the vendor and amount.

For example, if the invoice number is different but the same vendor has an invoice for the exact same amount, the system can flag it for further checking.

### File Hash

I also generate a SHA-256 hash for every uploaded file.

This means that if someone tries to upload the exact same file again, the system can identify it based on the file itself.

---

# ✅ Three-Stage Approval Workflow

The assessment required three approval stages, so I implemented the workflow as:

```text
Upload Document
       ↓
Stage 1 - Reviewer
       ↓
Stage 2 - Manager
       ↓
Stage 3 - Finance
       ↓
Approved
```

Each stage has to be completed before the next one becomes available.

### Stage 1 — Reviewer

The Reviewer checks the document first.

If they approve it:

```text
Pending → Pending Manager
```

If they reject it:

```text
Pending → Rejected
```

### Stage 2 — Manager

The Manager can only act after Stage 1 has been approved.

If approved:

```text
Pending Manager → Pending Finance
```

### Stage 3 — Finance

Finance performs the final approval.

If approved:

```text
Pending Finance → Approved
```

The backend checks these rules as well, so the workflow cannot simply be bypassed from the frontend.

---

# 📊 Reports

I added a Reports section so that the invoice information stored in the system can actually be used.

The reports can be filtered by:

* Date range
* Vendor
* Approval status
* Amount

I included different types of reporting, including:

* Spend summary
* Vendor analysis
* Approval status
* VAT/tax information

I also added the ability to export reports to:

* PDF
* Excel

---

# 🧠 AI Insights

I added an **AI Insights** section to give more value to the information being stored in the system.

Instead of only showing invoices, the system looks at the available financial data and provides insights around things such as:

* Spending trends
* Vendor spending
* High-value transactions
* VAT trends
* Possible anomalies
* Overall spending activity

I placed AI Insights directly in the main navigation so it is easy to access.

---

# 🔒 Security and Authorization

Security was something I paid attention to while building the application.

I used:

* ASP.NET Core Identity
* JWT authentication
* Role-based authorization
* Protected API endpoints
* File validation
* SHA-256 file hashing
* Backend approval authorization

One important part of the implementation is that I did not rely only on the frontend to control permissions.

For example, even if someone tried to directly call the approval API, the backend checks whether that user is actually authorised to perform that approval stage.

This is important because hiding a button in React alone would not be enough to secure the application.

---

# 🛠️ Technologies I Used

### Frontend

* React
* JavaScript
* Vite
* React Router
* Axios
* CSS

### Backend

* C#
* ASP.NET Core Web API
* ASP.NET Core Identity
* JWT
* Entity Framework Core

### Database

* SQLite
* Entity Framework Core migrations

### Other

* PDF text extraction
* SHA-256 hashing
* PDF report generation
* Excel report generation

---

# 🏗️ How the Application Works

The overall flow I implemented is:

```text
User Login
    ↓
Upload Invoice / Credit Note
    ↓
File Validation
    ↓
Invoice Data Extraction
    ↓
Duplicate Check
    ↓
Create Approval Workflow
    ↓
Reviewer
    ↓
Manager
    ↓
Finance
    ↓
Approved / Rejected
    ↓
Reports
    ↓
AI Insights
```

---

# 🧪 How to Test the Main Workflow

If you want to test the full approval process, I recommend using the accounts in this order.

### 1. Admin

Log in as:

```text
Email: admin@test.com
Password: Admin@12345
```

Upload an invoice or credit note.

### 2. Reviewer

Log in as:

```text
Email: reviewer@test.com
Password: N4@zT8!pQ6#Wm2$K
```

Approve the document at Stage 1.

The document should then move to:

```text
Pending Manager
```

### 3. Manager

Log in as:

```text
Email: manager1@test.com
Password: M8#qT3@vN7!xR5$K
```

Approve the document at Stage 2.

The document should then move to:

```text
Pending Finance
```

### 4. Finance

Log in as:

```text
Email: finance1@test.com
Password: F7@xR2!mQ9#kT4$L
```

Approve the document at Stage 3.

The final document status should become:

```text
Approved
```

### 5. Viewer

Finally, log in as:

```text
Email: viewer1@test.com
Password: V9!kQ4#tL7@xM2$R
```

The Viewer should be able to see the available information but should not have approval or upload permissions.

---

# 🚀 Running the Project

## Backend

Open a terminal in:

```text
Backend/Document management system/Document management system
```

Run:

```bash
dotnet restore
dotnet build
dotnet run
```

## Frontend

Open another terminal in:

```text
Frontend/document-management-client
```

Run:

```bash
npm install
npm run dev
```

Then open the URL provided by Vite.

---

# 📌 Final Note

I approached this assessment by building the system step by step, starting with the database and authentication and then adding document management, extraction, duplicate checking, approval workflow, reporting and AI Insights.

My main focus was not just getting the individual features working, but making sure that the different parts of the system work together. For example, when a document is uploaded, it is checked for duplicates, its invoice information is extracted, and the three-stage approval workflow is created automatically.

There are still areas I would improve for a production version, such as full OCR support for scanned documents, more detailed audit logging, cloud storage and additional automated testing. However, the main functionality requested in the assessment has been implemented.
