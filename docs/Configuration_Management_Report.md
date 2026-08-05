# Configuration Management Report
## Queue Backlog Intelligence System (QBIS)

| Field | Value |
|---|---|
| **Document ID** | CM-RPT-001 |
| **Version** | 1.0 |
| **Date** | 2026-08-04 |
| **Author** | Shweta Patel |
| **Repository** | QueueBacklogIntelligence (GitHub, `main`) |
| **Report Type** | Living Configuration Management Report |
| **Scope** | Full repository — backend, frontend, infrastructure, documentation |

---

## Table of Contents

1. [Executive Assessment](#1-executive-assessment)
2. [Repository and Version Control Environment](#2-repository-and-version-control-environment)
3. [Repository Structure](#3-repository-structure)
4. [Configuration Items](#4-configuration-items)
5. [Branching Strategy](#5-branching-strategy)
6. [Change Control Process](#6-change-control-process)
7. [Baseline Management](#7-baseline-management)
8. [Testing and Quality Gates](#8-testing-and-quality-gates)
9. [CI/CD and Automation](#9-cicd-and-automation)
10. [Release and Version Management](#10-release-and-version-management)
11. [Dependency and Environment Management](#11-dependency-and-environment-management)
12. [Traceability and Audit Trail](#12-traceability-and-audit-trail)
13. [Configuration Management Risks](#13-configuration-management-risks)
14. [Technical Debt](#14-technical-debt)
15. [Current Repository Maturity Assessment](#15-current-repository-maturity-assessment)
16. [Missing or Partially Implemented CM Artifacts](#16-missing-or-partially-implemented-cm-artifacts)
17. [Recommended Next Improvements](#17-recommended-next-improvements)
18. [Document Revision History](#18-document-revision-history)

---

## 1. Executive Assessment

QBIS is a well-structured solo development project that demonstrates solid documentation discipline and careful secrets management. The repository contains 34 commits across a 48-day development window (2026-06-11 to 2026-07-28), progressing through 12 annotated semantic version tags (v1.0.0 → v1.4.1). A single developer, Shweta Patel, authored all commits directly to the `main` branch.

**Strengths identified:**

- Comprehensive documentation suite (README, CHANGELOG, PRD, Test Plan, Risk Report, Architecture, API Reference, Known Issues)
- Consistent semantic versioning with annotated git tags and a maintained CHANGELOG
- Proper secrets management — production credentials are gitignored; example templates are committed
- Clean project structure with a logical separation of backend, frontend, docs, and infrastructure concerns
- `package-lock.json` committed, ensuring reproducible frontend builds

**Primary gaps:**

- No automated CI/CD pipeline — no `.github/workflows/` directory exists; all builds and deployments are manual
- No automated tests — testing relies entirely on manually executed bash scripts; zero automated assertions run on commit
- Single-branch workflow with no pull requests, branch protection, or code review — all 34 commits go directly to `main`
- No GitHub Releases — annotated tags exist but GitHub Release objects with release notes are not created
- CORS configuration hard-codes `localhost` origins; no staging or production CORS configuration is committed

**Overall maturity rating:** Level 2 — Managed (defined processes exist; not yet automated or enforced)

---

## 2. Repository and Version Control Environment

| Attribute | Value |
|---|---|
| **Platform** | GitHub |
| **VCS** | Git |
| **Default branch** | `main` |
| **Total branches** | 1 (main only) |
| **Total commits** | 34 |
| **First commit** | 2026-06-11 (`9c8b6d6` — Initial commit) |
| **Most recent commit** | 2026-07-28 (`b0ccb01` — docs: Test Plan Risk-Mitigation Matrix v1.3) |
| **Annotated tags** | 12 (v1.0.0 through v1.4.1) |
| **Pull requests** | 0 — all work committed directly to `main` |
| **GitHub Releases** | 0 — tags exist; Release objects not created |
| **GitHub Actions workflows** | 0 — no `.github/workflows/` directory |
| **Contributors** | 1 (Shweta Patel) |

### 2.1 Configuration Files Tracked at Root

| File | Purpose | Status |
|---|---|---|
| `.gitignore` | Excludes secrets, build artifacts, local settings | Committed |
| `.gitattributes` | Line-ending normalization | Committed |
| `.env.example` | Template for root-level environment variables | Committed |
| `start.sh` | Local development startup script | Committed |
| `docker-compose.yml` | Production container orchestration | Committed |
| `docker-compose.dev.yml` | Development container orchestration | Committed |
| `README.md` | Project overview and setup guide | Committed |
| `CHANGELOG.md` | Version history (v1.0.0 – v1.4.0) | Committed |
| `KNOWN_ISSUES.md` | Active limitations and known bugs | Committed |
| `PROJECT_CONTEXT.md` | Background context and design decisions | Committed |

---

## 3. Repository Structure

```
QueueBacklogIntelligence/
├── backend/                        # .NET 8 Azure Functions (isolated worker)
│   ├── Functions/                  # 5 Azure Function entry points
│   │   ├── CollectorFunction.cs
│   │   ├── AnalyzerFunction.cs
│   │   ├── AlertDispatcherFunction.cs
│   │   ├── CleanupFunction.cs
│   │   └── DashboardFunction.cs
│   ├── Services/                   # Business logic layer
│   │   ├── CollectorService.cs
│   │   ├── AnalyzerService.cs
│   │   ├── AlertService.cs
│   │   └── CleanupService.cs
│   ├── Models/                     # Domain models (shared DTOs)
│   ├── Repository/                 # Azure Table Storage abstraction
│   ├── Tests/
│   │   ├── test_scenarios.sh       # 16 manual bash test scenarios
│   │   ├── test-helpers.sh         # Shared assertion helpers
│   │   └── TestResults/            # Gitignored — excluded from repo
│   ├── QueueBacklogIntelligence.csproj
│   ├── host.json                   # Azure Functions host configuration
│   ├── local.settings.json.example # Secret template (committed)
│   └── local.settings.json         # Gitignored (production secrets)
├── frontend/                       # React 18 + Vite 6 SPA
│   ├── src/
│   │   ├── auth/                   # MSAL authentication layer
│   │   ├── components/             # Sidebar, shared UI
│   │   ├── pages/                  # Overview, QueueDetail, Settings
│   │   └── hooks.js                # useFetch shared data hook
│   ├── package.json
│   ├── package-lock.json           # Committed — reproducible builds
│   ├── vite.config.js
│   ├── Dockerfile
│   ├── nginx.conf
│   ├── .env.development            # Gitignored (local VITE_ vars)
│   └── .env.production             # Gitignored (production VITE_ vars)
├── docs/
│   ├── ARCHITECTURE.md
│   ├── API_REFERENCE.md
│   ├── ANALYZER_PIPELINE.md
│   ├── Product_Requirements_Document.md
│   ├── Risk_Management_Report.md
│   ├── Software_Test_Plan_and_Report.md
│   ├── Configuration_Management_Report.md  ← this document
│   └── screenshots/
├── .github/
│   └── prompts/                    # AI prompt templates (PRD, Test Plan)
│       ├── generate-prd.prompt.md
│       └── Generate-Software-Test-Plan-and-Report.prompt.md
├── .gitignore
├── .gitattributes
├── .env.example
├── docker-compose.yml
├── docker-compose.dev.yml
├── start.sh
├── README.md
├── CHANGELOG.md
├── KNOWN_ISSUES.md
└── PROJECT_CONTEXT.md
```

**Assessment:** The structure is well-organized with clear separation of concerns. `backend/Tests/TestResults/` is gitignored (correct — output artifacts should not be versioned). The `docs/` folder is systematically maintained and now contains the full documentation suite required by the Q1/Q2/Q3 framework.

---

## 4. Configuration Items

A Configuration Item (CI) is any artifact whose change requires controlled versioning and traceability.

### 4.1 Backend CIs

| CI | File/Artifact | Version Source | Change Mechanism |
|---|---|---|---|
| Azure Functions host | `backend/host.json` | Git commit | Edit + commit |
| NuGet dependency manifest | `backend/QueueBacklogIntelligence.csproj` | Exact versions in `.csproj` | Edit + commit |
| Function runtime settings template | `backend/local.settings.json.example` | Git commit | Edit + commit |
| Collector timer cadence | `CollectorFunction.cs` — `0 */1 * * * *` | Source code | Edit + commit |
| Analyzer timer cadence | `AnalyzerFunction.cs` — `0 */1 * * * *` | Source code | Edit + commit |
| Cleanup timer cadence | `CleanupFunction.cs` — `0 0 2 * * *` | Source code | Edit + commit |
| Retention windows | `CleanupFunction.cs` constants (24 h / 90 d / 90 d) | Source code | Edit + commit |
| Alert cooldown / severity thresholds | `AnalyzerService.cs` constants | Source code | Edit + commit |
| CORS allowed origins | `backend/host.json` + `DashboardFunction.cs` | Source code | Edit + commit |

**Note:** Production secrets (`ServiceBusConnectionString`, `StorageConnectionString`, SMTP credentials) live in `local.settings.json` (local) and Azure Application Settings (cloud). Neither is committed. The template `local.settings.json.example` documents required keys without values.

### 4.2 Frontend CIs

| CI | File/Artifact | Version Source | Change Mechanism |
|---|---|---|---|
| npm dependency manifest | `frontend/package.json` | `^` semver ranges | Edit + commit |
| npm lockfile | `frontend/package-lock.json` | Pinned exact versions | `npm install` generates; committed |
| Vite build config | `frontend/vite.config.js` | Git commit | Edit + commit |
| nginx reverse proxy config | `frontend/nginx.conf` | Git commit | Edit + commit |
| MSAL configuration | `frontend/src/auth/msalConfig.js` | Runtime env vars | Edit + commit |
| Auth toggle | `VITE_AUTH_ENABLED` env var | `.env.*` (gitignored) | Per-environment |
| API base URL | `VITE_API_URL` env var | `.env.*` (gitignored) | Per-environment |
| Azure AD credentials | `VITE_AZURE_CLIENT_ID`, `VITE_AZURE_TENANT_ID`, `VITE_AZURE_API_SCOPE` | `.env.*` (gitignored) | Per-environment |

### 4.3 Infrastructure CIs

| CI | File/Artifact | Notes |
|---|---|---|
| Production compose | `docker-compose.yml` | nginx + backend + frontend containers |
| Development compose | `docker-compose.dev.yml` | Hot-reload mounts |
| Backend Dockerfile | `backend/Dockerfile` | .NET 8 SDK → runtime multi-stage |
| Frontend Dockerfile | `frontend/Dockerfile` | Node build → nginx serve |

### 4.4 Documentation CIs

| Document | Current Version | File |
|---|---|---|
| Product Requirements Document | v1.2 | `docs/Product_Requirements_Document.md` |
| Software Test Plan and Report | v1.3 | `docs/Software_Test_Plan_and_Report.md` |
| Risk Management Report | v1.0 | `docs/Risk_Management_Report.md` |
| Configuration Management Report | v1.0 | `docs/Configuration_Management_Report.md` |
| Architecture | — | `docs/ARCHITECTURE.md` |
| API Reference | — | `docs/API_REFERENCE.md` |
| Analyzer Pipeline | — | `docs/ANALYZER_PIPELINE.md` |

---

## 5. Branching Strategy

### 5.1 Current State

The repository uses a **single-branch workflow** on `main`. All 34 commits were pushed directly to `main` with no feature branches, no integration branch, and no release branches.

| Metric | Value |
|---|---|
| Active branches | 1 (`main`) |
| Merged branches | 0 |
| Deleted branches | 0 |
| Pull requests opened | 0 |
| Direct commits to main | 34 of 34 |

**Assessment:** Acceptable for a solo project with a single developer. The risk is that any commit — including one with a defect — immediately becomes the HEAD of `main`. No review gate or staging opportunity exists between authoring and publishing.

### 5.2 Recommended Future State

For a multi-developer project or one requiring formal change review, a lightweight trunk-based strategy is recommended:

```
main  ←──────── (protected; no direct push)
         ↑
   feature/xxx   (short-lived; PR → main; CI required to merge)
```

Under the current solo workflow, branch protection rules with at least a passing CI status check would provide a safety net without requiring a second reviewer.

---

## 6. Change Control Process

### 6.1 Current Process

All changes follow an **informal direct-commit workflow**:

1. Developer edits files locally
2. Files are staged with `git add`
3. Commit is authored with a descriptive message
4. Commit is pushed directly to `main`
5. No automated build, test, or lint check runs before or after push

No ticket system, issue tracker, or change request form is linked in the repository. Changes are described exclusively through commit messages.

### 6.2 Commit Message Quality Assessment

Commit messages follow a **conventional commits** style (`type(scope): description`) for the majority of commits:

| Convention | Example from repo | Consistency |
|---|---|---|
| `feat:` — new features | `feat: add Sign out button to Sidebar for MSAL logout` | Consistent |
| `fix:` — bug fixes | `fix(analyzer): DLQGrowth detection and Unknown/Critical severity bug` | Consistent |
| `docs:` — documentation | `docs: replace Risk_Management_Report.docx with .md format` | Consistent |
| `test:` — test changes | `test: add TC-11–TC-16 curl scenarios for Dashboard API` | Consistent |
| `chore:` — housekeeping | `chore: add .github/prompts folder with PRD generation prompt` | Consistent |

**Exception:** Two commits lack conventional prefix: `"Updated Risk Management Report"` (`28a9a49`) and `"Added Risk Management Report"` (`e4ddae3`). Both are documentation commits that predate the documentation workflow being fully established.

### 6.3 Gaps

- No pre-commit hooks enforce message format, linting, or tests
- No branch protection rules require passing checks before merge
- No code review — changes are self-approved by the sole developer
- No change request record outside of commit messages

---

## 7. Baseline Management

### 7.1 Baseline History

Baselines are implemented via **annotated git tags** with semantic versioning. All 12 tags are annotated (not lightweight), meaning they carry a tagger name, date, and message.

| Tag | Date | Key Changes |
|---|---|---|
| v1.0.0 | 2026-06-11 | Initial release — 5 Azure Functions, React dashboard, Docker, 10 test scenarios |
| v1.0.1 | 2026-06-20 | Docs: PROJECT_CONTEXT.md additions |
| v1.1.0 | 2026-07-13 | Teams dual-format alerts, email alerts via SMTP, `idleThreshold` fix, OpenAPI removed |
| v1.1.1 | 2026-07-13 | Test hardening (TC-01, TC-03, TC-05, TC-09, TC-10), new helper function |
| v1.2.0 | 2026-07-13 | Frontend redesign — sidebar, multi-queue Overview, URL-driven Queue Detail |
| v1.2.1 | 2026-07-13 | CHANGELOG.md added |
| v1.3.0 | 2026-07-13 | Azure AD authentication (Easy Auth + MSAL React v5) |
| v1.3.1 | 2026-07-14 | KNOWN_ISSUES.md added; orphaned Dashboard.jsx removed |
| v1.3.2 | 2026-07-16 | CORS `Authorization` header fix, login redirect fix |
| v1.3.3 | 2026-07-16 | DLQGrowth burst fix, `Unknown` severity guard |
| v1.4.0 | 2026-07-20 | CleanupFunction (daily purge), test script menu fix |
| v1.4.1 | 2026-07-20 | Docs: truncated KNOWN_ISSUES.md curl command restored |

**Assessment:** The baseline cadence is appropriate — minor fixes receive patch tags (`.x`) while significant functional additions receive minor version bumps (`.x.0`). The CHANGELOG accurately mirrors the tag content.

### 7.2 Gap: GitHub Releases

While annotated tags exist locally and on the remote, no GitHub Release objects have been created. GitHub Releases add a release notes page accessible to team members and allow binary assets (build artifacts) to be attached. This is a low-effort improvement.

---

## 8. Testing and Quality Gates

### 8.1 Test Infrastructure

| Attribute | Value |
|---|---|
| Test framework | None — manual bash scripts only |
| Automated unit tests | 0 |
| Automated integration tests | 0 |
| Manual test scenarios | 16 (TC-01 through TC-16) |
| Test runner | `backend/Tests/test_scenarios.sh` (interactive menu) |
| Test helpers | `backend/Tests/test-helpers.sh` |
| Test results storage | `backend/Tests/TestResults/` — gitignored |
| CI test execution | None |

### 8.2 Test Scenario Coverage

| Scenario Group | TC Numbers | FR Coverage | Status |
|---|---|---|---|
| Core queue lifecycle | TC-01 – TC-10 | FR-1.x.x, FR-2.x.x, FR-3.x.x | Defined; 2 executed sessions in TestResults/ |
| Dashboard API (FR-5.x.x) | TC-11, TC-12, TC-13 | GET /queues, history, alerts | Defined; not yet executed |
| Queue Config CRUD (FR-4.x.x) | TC-14, TC-15, TC-16 | POST, PUT, DELETE /queues | Defined; not yet executed |
| CleanupFunction (FR-6.x.x) | None | Timer-driven, 24h window hard to script | Not scenario-tested |
| Auth / Easy Auth (FR-7.x.x) | None | Azure-only infrastructure feature | Not scenario-tested |

**Coverage:** 11 of 20 functional requirements have a linked test scenario (55%). See `docs/Software_Test_Plan_and_Report.md` Section 17 for the full Risk-Mitigation traceability matrix.

### 8.3 Quality Gate Enforcement

There are currently **no enforced quality gates**. No pre-commit hooks, no CI step, and no branch protection rule prevents a commit from reaching `main` with failing tests or broken syntax.

---

## 9. CI/CD and Automation

### 9.1 Current State

| Pipeline Component | Status |
|---|---|
| `.github/workflows/` directory | Does not exist |
| Build automation | None |
| Test automation | None |
| Lint/format check | None |
| Docker image build | Manual (`docker-compose build`) |
| Azure Function deployment | Manual (Azure Portal or `func azure functionapp publish`) |
| Frontend deployment | Manual |
| Secrets injection | Manual (Azure Application Settings, local `.env` files) |

The only automation present is the **`start.sh` script**, which starts Docker Compose for local development. This is a developer convenience script, not a CI/CD artifact.

### 9.2 Deployment Workflow (Inferred)

Based on the committed files and project structure, the inferred deployment workflow is:

**Local development:**
1. Copy `backend/local.settings.json.example` → `backend/local.settings.json`, fill in secrets
2. Copy `frontend/.env.development` (not committed) with `VITE_AUTH_ENABLED=false`
3. Run `./start.sh` or `docker-compose -f docker-compose.dev.yml up`

**Production deployment:**
1. Build backend: `dotnet publish` → deploy to Azure Functions App
2. Build frontend: `npm run build` → deploy static files to Azure Static Web Apps or serve via nginx container
3. Configure Azure Application Settings with production secrets
4. Enable Azure AD Easy Auth on the Function App in Azure Portal

None of these steps are automated or documented as runnable scripts.

---

## 10. Release and Version Management

### 10.1 Versioning Scheme

The project follows **Semantic Versioning 2.0.0** (`MAJOR.MINOR.PATCH`):

- **MAJOR** — no bump yet; the project is pre-1.0 stable by the SemVer convention, but has been using 1.x since initial release, treating 1.0.0 as the first public-facing baseline
- **MINOR** — bumped for significant new functional capabilities (authentication in v1.3.0; CleanupFunction in v1.4.0)
- **PATCH** — bumped for bug fixes, documentation updates, and test hardening

### 10.2 CHANGELOG Maintenance

`CHANGELOG.md` is manually maintained and current through v1.4.0. Format follows Keep a Changelog conventions with `### Added`, `### Fixed`, and `### Removed` subsections per release. Content is substantive and technically detailed — each entry explains the root cause of fixes, not just what changed.

**Gap:** v1.4.1 appears as an annotated tag but is not reflected in `CHANGELOG.md` (the file ends at v1.4.0). The v1.4.1 change (restoring a truncated KNOWN_ISSUES.md entry) should be appended.

### 10.3 Version Declaration Locations

| Location | Declared Version | Notes |
|---|---|---|
| Git tag | v1.4.1 | Most recent annotated tag |
| CHANGELOG.md | v1.4.0 | Lagging — v1.4.1 entry missing |
| `frontend/package.json` | 1.0.0 | Static; not updated with releases |
| `backend/QueueBacklogIntelligence.csproj` | No version property | Not declared in project file |
| `docs/Product_Requirements_Document.md` | v1.2 | Document version, not product version |

**Recommendation:** Add a `<Version>` property to the `.csproj` and keep `package.json` `"version"` field in sync with git tags. This enables `dotnet publish` and `npm version` to reflect the correct release.

---

## 11. Dependency and Environment Management

### 11.1 Backend Dependencies (.NET / NuGet)

All NuGet packages are pinned to **exact versions** in the `.csproj` file:

| Package | Version | Purpose |
|---|---|---|
| Azure.Data.Tables | 12.11.0 | Table Storage CRUD |
| Azure.Identity | 1.13.2 | Managed identity / credential chain |
| Azure.Messaging.ServiceBus | 7.18.4 | Service Bus administration client |
| Azure.Monitor.Query | 1.4.0 | Azure Monitor metrics query |
| Microsoft.Azure.Functions.Worker | 2.0.0 | Isolated worker host |
| Microsoft.Azure.Functions.Worker.Extensions.Http | 3.2.0 | HTTP trigger binding |
| Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore | 2.0.0 | ASP.NET Core integration |
| Microsoft.Azure.Functions.Worker.Extensions.Timer | 4.3.1 | Timer trigger binding |
| Microsoft.Azure.Functions.Worker.Sdk | 2.0.0 | Build SDK |
| Microsoft.Extensions.Logging | 8.0.1 | Structured logging |

**Assessment:** Exact version pinning ensures reproducible builds without a lock file mechanism (NuGet uses `.csproj` directly). The Azure SDK packages are reasonably current. No automatic vulnerability scanning is configured.

### 11.2 Frontend Dependencies (npm)

Frontend dependencies use `^` (caret) semver ranges in `package.json`, allowing automatic minor/patch updates:

| Package | Range | Resolved (lock) | Purpose |
|---|---|---|---|
| @azure/msal-browser | ^5.17.0 | pinned in lock | MSAL core |
| @azure/msal-react | ^5.5.2 | pinned in lock | MSAL React hooks |
| react | ^18.3.1 | pinned in lock | UI framework |
| react-dom | ^18.3.1 | pinned in lock | React DOM renderer |
| react-router-dom | ^7.16.0 | pinned in lock | Client-side routing |
| recharts | ^2.13.3 | pinned in lock | Chart library |

`package-lock.json` is committed, which locks exact resolved versions. Builds are reproducible via `npm ci`. The `^` ranges in `package.json` mean that running `npm install` without the lock file would resolve to newer minor versions — the lock file is the authoritative version record.

**Assessment:** The combination of `^` ranges + committed lock file is the npm best practice for application code.

### 11.3 Environment Variable Management

| Variable | Scope | Storage | Committed? |
|---|---|---|---|
| `ServiceBusConnectionString` | Backend | `local.settings.json` (local), Azure App Settings (prod) | No — gitignored |
| `StorageConnectionString` | Backend | `local.settings.json` (local), Azure App Settings (prod) | No — gitignored |
| `SmtpHost`, `SmtpPort`, `SmtpUser`, `SmtpPassword`, `SmtpFromAddress` | Backend | Same as above | No — gitignored |
| `VITE_AUTH_ENABLED` | Frontend | `.env.development` / `.env.production` | No — gitignored |
| `VITE_AZURE_CLIENT_ID` | Frontend | `.env.development` / `.env.production` | No — gitignored |
| `VITE_AZURE_TENANT_ID` | Frontend | Same | No — gitignored |
| `VITE_AZURE_API_SCOPE` | Frontend | Same | No — gitignored |
| `VITE_API_URL` | Frontend | Same | No — gitignored |

**Assessment:** Secrets handling is correct. All credentials and environment-specific values are gitignored. Example templates (`local.settings.json.example`, `.env.example`) document required keys. The `.gitattributes` file is committed, ensuring consistent line endings across platforms.

**Gap noted:** `.env.development` and `.env.production` are listed in `.gitignore` but the files exist on the developer's local machine. A new developer cloning the repository would need to create these files manually. The `.env.example` documents the required variables but the separation between backend and frontend variable documentation could be clearer.

---

## 12. Traceability and Audit Trail

### 12.1 Commit-Level Traceability

Git provides a complete audit trail of every change:

- All 34 commits carry author name, email, date, and descriptive message
- All commits are attributed to Shweta Patel
- Commit messages consistently describe the **why** and the **what** — notably including root cause explanations for bug fixes (e.g., commit `0922bb9` and `0ac6bf3`)

### 12.2 Cross-Document Traceability

The documentation suite establishes explicit cross-document traceability:

```
Risk Management Report (R1–R8)
    ↓
Product Requirements Document (UE IDs → FR IDs → PR IDs)
    ↓
Software Test Plan and Report (FR IDs → TC IDs → Execution Results)
    ↑
Configuration Management Report (this document)
```

`docs/Software_Test_Plan_and_Report.md` Section 9 (Traceability Matrix) and Section 17 (Risk-Mitigation Matrix) provide the explicit FR → TC → Risk mapping.

### 12.3 Audit Trail Gaps

| Gap | Impact |
|---|---|
| No issue/ticket linkage in commits | Cannot navigate from a commit to the original bug report or requirement |
| No pull request history | No record of who reviewed changes (N/A for solo project; relevant if team grows) |
| `v1.4.1` missing from CHANGELOG.md | Release history is incomplete |
| Test execution results are gitignored | Historical test results are not auditable |

---

## 13. Configuration Management Risks

| Risk ID | Risk Description | Likelihood | Impact | Severity | Mitigation |
|---|---|---|---|---|---|
| CMR-1 | No CI/CD means a broken build is not detected until a developer runs it manually | Medium | High | **High** | Add GitHub Actions workflow with `dotnet build` and `npm run build` |
| CMR-2 | Direct commits to `main` with no quality gate; a bad commit immediately becomes HEAD | Medium | High | **High** | Enable branch protection rules requiring CI pass before merge |
| CMR-3 | `host.json` CORS allows only `localhost` origins; production CORS is configured in Azure but not in the committed artifact | Low | Medium | **Medium** | Add environment-based CORS config or document the Azure Portal step |
| CMR-4 | Frontend `^` semver ranges allow `npm install` (without lock) to pull in breaking minor versions | Low | Medium | **Medium** | Use `npm ci` in all build/deploy steps; never run bare `npm install` in CI |
| CMR-5 | No dependency vulnerability scanning; a CVE in any of the 10+ packages would not be detected automatically | Medium | Medium | **Medium** | Enable GitHub Dependabot for both NuGet and npm |
| CMR-6 | Timer cadences (Collector, Analyzer, CleanupFunction) are hardcoded constants in source; changing them requires a code deploy | Low | Low | **Low** | Consider externalizing to Application Settings if frequency tuning is needed in production |
| CMR-7 | Single developer — no bus factor mitigation; repository knowledge is concentrated | Medium | High | **High** | Documentation mitigates partially; onboarding docs would help |
| CMR-8 | `v1.4.1` tag exists but CHANGELOG entry missing — minor history gap | Low | Low | **Low** | Append v1.4.1 entry to CHANGELOG.md |

---

## 14. Technical Debt

### 14.1 Known Issues (from KNOWN_ISSUES.md)

| ID | Issue | Status |
|---|---|---|
| KI-01 | Azure Monitor ingestion delay (2–4 min) means first snapshot after a burst may under-report | Active limitation |
| KI-02 | Alert cooldown — consecutive OK readings required to de-escalate; can delay "all clear" notification | Active limitation |
| KI-03 | `AuthorizationLevel.Anonymous` on all HTTP triggers — API is open locally; relies on Easy Auth in Azure | Active (documented) |

### 14.2 Structural Technical Debt

| Item | Location | Description |
|---|---|---|
| CORS hard-coded to `localhost` | `backend/host.json` | Production CORS must be reconfigured in Azure Portal; there is no committed production host configuration |
| SMTP uses synchronous `SmtpClient` | `backend/Services/AlertService.cs` | `System.Net.Mail.SmtpClient` is a legacy API; `MailKit` is the recommended replacement |
| No dependency injection for `IRepository` | Services layer | Services directly instantiate repository clients rather than receiving them via DI, making unit testing difficult |
| Test results gitignored | `backend/Tests/TestResults/` | Historical test execution records cannot be audited from the repository |
| `frontend/package.json` version stuck at `1.0.0` | `frontend/package.json` | Package version does not track product release version |

### 14.3 Documentation Technical Debt

| Item | Description |
|---|---|
| CHANGELOG missing v1.4.1 entry | The most recent git tag is not reflected in CHANGELOG.md |
| CORS production setup not documented in README | A developer deploying to Azure has no committed instructions for configuring production CORS |
| No developer onboarding guide | No "from-zero" guide covering Azure AD app registration steps end-to-end |

---

## 15. Current Repository Maturity Assessment

### 15.1 Capability Dimension Ratings

The following assessment uses a 1–4 maturity scale: 1 = Ad hoc, 2 = Managed, 3 = Defined and enforced, 4 = Optimized.

| Dimension | Rating | Evidence |
|---|---|---|
| **Version control usage** | 3 | Consistent commits, annotated tags, meaningful messages, `.gitignore` properly maintained |
| **Secrets management** | 3 | Production secrets gitignored; example templates committed; no leaked credentials detected |
| **Dependency management** | 3 | NuGet exact pinning; npm lock file committed; consistent package structure |
| **Documentation coverage** | 3 | Full suite: PRD, Test Plan, Risk Report, CM Report, Architecture, API Reference, CHANGELOG, KNOWN_ISSUES |
| **Branching and change control** | 1 | Single branch; direct commits; no PRs; no reviews; no branch protection |
| **Testing and quality gates** | 1 | Manual scripts only; zero automated tests; no CI enforcement |
| **CI/CD automation** | 1 | No pipelines exist |
| **Release management** | 2 | SemVer tags and CHANGELOG maintained; GitHub Releases not created; version not synchronized across package manifests |
| **Traceability** | 3 | Cross-document FR → TC → Risk chain established; commit messages describe rationale |
| **Security posture** | 2 | Secrets protected; Easy Auth in production; no automated vulnerability scanning; CORS localhost-only |

**Composite score: 2.2 / 4 — Managed**

### 15.2 Comparison to Assignment Criteria

| Criterion | Status | Notes |
|---|---|---|
| Repository hosted on version control | Met | GitHub, `main` branch |
| Semantic versioning with tags | Met | 12 annotated tags, SemVer compliant |
| CHANGELOG maintained | Partially met | Missing v1.4.1 |
| CI/CD pipeline | Not met | No `.github/workflows/` |
| Automated tests | Not met | Manual scripts only |
| Branch protection / PR workflow | Not met | Direct commits to main |
| Secrets excluded from repo | Met | `.gitignore` correct; no committed secrets detected |
| Documentation suite | Met | PRD, Test Plan, Risk, CM, Architecture, API Reference |
| Dependency lock files | Met | `package-lock.json` committed; NuGet exact pinning |
| GitHub Releases | Not met | Tags exist; Release objects not created |

---

## 16. Missing or Partially Implemented CM Artifacts

### Priority: High

| Artifact | Description | Gap |
|---|---|---|
| GitHub Actions workflow | `.github/workflows/ci.yml` — build + test on push | Does not exist |
| Branch protection rules | Require CI pass before push to `main` | Not configured |
| Dependabot configuration | `.github/dependabot.yml` for NuGet and npm | Does not exist |

### Priority: Medium

| Artifact | Description | Gap |
|---|---|---|
| GitHub Releases | Release notes pages tied to each annotated tag | Not created for any tag |
| Production host configuration | `host.json` with non-localhost CORS for Azure deployment | Not committed |
| `.csproj` version property | `<Version>` element synchronized with git tags | Absent |
| Deployment runbook | Step-by-step production deployment procedure | Not committed |
| CHANGELOG v1.4.1 entry | Release notes for the most recent tag | Missing |

### Priority: Low (Future)

| Artifact | Description |
|---|---|
| Pre-commit hooks | `husky` or `lefthook` for commit message format and lint enforcement |
| Automated unit tests | xUnit for .NET; Vitest for React components |
| Container registry configuration | Push-on-tag workflow to GitHub Container Registry or Azure Container Registry |
| Environment-specific `host.json` | Separate CORS configuration per environment |

---

## 17. Recommended Next Improvements

### High Priority

1. **Add a minimal GitHub Actions CI workflow**

   Create `.github/workflows/ci.yml` that triggers on every push to `main`:

   ```yaml
   on: [push, pull_request]
   jobs:
     build-backend:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
         - uses: actions/setup-dotnet@v4
           with: { dotnet-version: '8.0.x' }
         - run: dotnet build backend/
     build-frontend:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
         - uses: actions/setup-node@v4
           with: { node-version: '20' }
         - run: cd frontend && npm ci && npm run build
   ```

   This catches build breakage without requiring test automation as a prerequisite.

2. **Enable GitHub branch protection on `main`**

   In Settings → Branches → Add rule for `main`: require status checks to pass before pushing. Once the CI workflow above exists, add it as a required check. This prevents broken commits reaching `main`.

3. **Enable GitHub Dependabot**

   Create `.github/dependabot.yml`:

   ```yaml
   version: 2
   updates:
     - package-ecosystem: "nuget"
       directory: "/backend"
       schedule: { interval: "weekly" }
     - package-ecosystem: "npm"
       directory: "/frontend"
       schedule: { interval: "weekly" }
   ```

   This automates security patch PRs for both ecosystems.

### Medium Priority

4. **Create GitHub Releases for all existing tags**

   Use `gh release create vX.Y.Z --notes "..."` for each of the 12 existing tags. Future releases can be scripted as part of a release workflow.

5. **Add v1.4.1 entry to CHANGELOG.md**

   Append a `## [v1.4.1] — 2026-07-20` section documenting the restored KNOWN_ISSUES.md curl command.

6. **Synchronize version across package manifests**

   - Add `<Version>1.4.1</Version>` to `backend/QueueBacklogIntelligence.csproj`
   - Update `"version": "1.4.1"` in `frontend/package.json`

7. **Document production deployment procedure**

   Add a `docs/DEPLOYMENT.md` covering: Azure resource prerequisites, Azure AD app registration steps, Function App deployment command, frontend static deployment, and Azure Application Settings required keys.

### Future (When Team Grows or Requires Formal Review)

8. **Introduce a feature-branch workflow**

   Require all changes to go through a short-lived branch and pull request. Even a solo developer benefits from the pull request review UI for self-review before merge.

9. **Add automated unit tests for AnalyzerService**

   The 9-step analysis pipeline (`AnalyzerService.cs`) has complex branching logic covering 8 root cause classifications. These are the highest-value targets for xUnit tests given that regressions in this layer are hard to catch with manual end-to-end scenarios.

10. **Container image push on release tag**

    Extend the GitHub Actions workflow to build and push Docker images to GitHub Container Registry (GHCR) when a `v*` tag is pushed. This enables repeatable deployment from a known image rather than rebuilding from source at deploy time.

---

## 18. Document Revision History

| Version | Date | Author | Changes |
|---|---|---|---|
| 1.0 | 2026-08-04 | Shweta Patel | Initial document — full repository inspection covering all 18 CM sections; 34-commit baseline through v1.4.1 |

---

*This is a living document. It should be updated when significant infrastructure changes occur — such as adding a CI/CD pipeline, migrating to a multi-branch workflow, or onboarding additional developers.*
