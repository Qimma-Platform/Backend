# 🎓 Qimmah Educational Platform — Backend Web API

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![EF Core 8](https://img.shields.io/badge/Entity%20Framework-Core%208-512BD4)](https://learn.microsoft.com/ef/)
[![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-CC292B?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server/)
[![JWT Auth](https://img.shields.io/badge/Security-JWT%20Bearer-black?logo=jsonwebtokens)](https://jwt.io/)
[![Paymob Integrated](https://img.shields.io/badge/Payments-Paymob%20EGP-0052CC)](https://paymob.com/)
[![SignalR](https://img.shields.io/badge/Realtime-SignalR-purple)](https://dotnet.microsoft.com/apps/aspnet/signalr)

> Robust, high-performance, enterprise-grade RESTful API powering the **Qimmah Smart Educational Platform** (منصة قمة التعليمية الذكية). Built with ASP.NET Core 8, Entity Framework Core, SQL Server, and SignalR for real-time live learning, student XP points, smart evaluations, and automated multi-channel financial processing.

---

## 🏗️ Architecture & Technology Stack

- **Framework**: ASP.NET Core 8 Web API (C# 12)
- **Data Access**: Entity Framework Core 8 with Code-First Migrations
- **Database Engine**: Microsoft SQL Server (LocalDB / Azure SQL / MSSQL Express)
- **Authentication**: JWT Bearer Tokens with Role-Based Authorization (`Student`, `Instructor`, `Admin`)
- **Real-Time Communication**: ASP.NET Core SignalR (`LiveSessionHub`)
- **Email Service**: MailKit & MimeKit over secure SMTP (Google App Password / SendGrid)
- **Payment Gateway**: Paymob Unified Checkout (Cards, Mobile Wallets, Fawry) with secure server-to-server Webhook callbacks
- **Password Security**: BCrypt.Net cryptographic hashing
- **API Documentation**: Swagger / OpenAPI (Swashbuckle)

---

## ✨ Core Modules & Functional Capabilities

### 1. 🔐 Security & Identity Management
- **Role Isolation**: Strict barrier preventing cross-portal login (Students/Teachers cannot access Admin endpoints, and Admins cannot use Student portals).
- **Password Reset Flow**: Email verification tokens with secure expiration and contextual redirection.
- **Audit Logging**: Comprehensive admin ledger (`AuditLogs`) tracking every administrative action with timestamp, admin ID, entity, and action details.

### 2. 📚 Course & Curriculum Engine
- **Hierarchical Structure**: Courses $\rightarrow$ Lessons $\rightarrow$ Video Resources $\rightarrow$ Quizzes & Assignments.
- **Video Embedding**: Streamlined direct URL streaming supporting YouTube, Vimeo, and direct MP4/WebM resources without server storage bottlenecks.
- **Drill-Down Filtering**:
  - Filter courses by assigned Instructor.
  - Filter courses by enrolled Student (`GET /api/admin/students/{id}/courses`).
  - Course enrollees manager for enrollment, verification, and unenrollment.

### 3. 📝 Assessments & Smart Evaluation
- **Interactive Quizzes**: Multiple-choice assessments with immediate server-side validation, score calculation, and XP rewards.
- **Assignments Workflow**:
  - Teacher assignment creation with multi-question formats.
  - Student submissions with submission status tracking.
  - Per-question teacher review: reveals student's chosen answer vs correct answer with visual tags (`✓ Correct Answer`).
- **Gamification & XP**: Automatic experience point accrual on course and assessment completion.

### 4. 💳 Financial System & Paymob Webhooks
- **Payment Flow**:
  1. Student initiates checkout for a paid course $\rightarrow$ Backend calculates discounted price in cents $\rightarrow$ Requests Paymob Unified Checkout token.
  2. Student completes payment via Credit Card, Mobile Wallet (Vodafone Cash, Orange, etc.), or Fawry.
  3. Paymob dispatches signed POST Webhook to `/api/payment/webhook`.
  4. System verifies transaction, creates `CourseEnrollment`, credits 80% to Instructor's wallet balance, reserves 20% platform share, creates ledger entry in `PaymentTransactions`, and emits real-time in-app notifications.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Microsoft SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads) (or LocalDB installed with Visual Studio)

### 1. Clone & Navigate
```bash
git clone https://github.com/Qimma-Platform/Backend.git
cd Backend
```

### 2. Configuration Setup
Create an `appsettings.json` file in the project root by copying the provided safe template:
```bash
cp appsettings.Example.json appsettings.json
```

Populate `appsettings.json` with your environment credentials:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ELearningKidsDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    "Key": "YOUR_SUPER_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG",
    "Issuer": "ELearningApp",
    "Audience": "ELearningAppUsers"
  },
  "EmailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "YOUR_GMAIL_APP_PASSWORD",
    "SenderEmail": "your-email@gmail.com"
  },
  "Paymob": {
    "SecretKey": "egy_sk_test_...",
    "PublicKey": "egy_pk_test_...",
    "IntegrationId": 0,
    "CardIntegrationId": "0",
    "FawryIntegrationId": "0",
    "WalletIntegrationId": "0"
  }
}
```

### 3. Run Database Migrations
```bash
dotnet ef database update
```

### 4. Build & Launch
```bash
dotnet build
dotnet run
```
The API will launch at `http://localhost:5162` with Swagger available at `http://localhost:5162/swagger`.

---

## 🌐 Cloud Deployment & Environment Variables

When deploying to cloud hosts (**Render**, **Azure**, **MonsterASP**, **Docker**, or **VPS**), **never commit `appsettings.json`**. Instead, configure Environment Variables using the double-underscore (`__`) notation:

| Environment Variable | Description |
| :--- | :--- |
| `ConnectionStrings__DefaultConnection` | Production SQL Server connection string |
| `Jwt__Key` | Secret key used to sign and verify JWT tokens |
| `EmailSettings__Username` | SMTP username |
| `EmailSettings__Password` | SMTP application password |
| `Paymob__SecretKey` | Paymob Secret Key (Test or Live) |
| `Paymob__PublicKey` | Paymob Public Key |

---

## 🛡️ License
Private and proprietary. Designed and maintained for the **Qimmah Educational Platform**. All rights reserved.
