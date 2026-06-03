# Security Review — OnlineExam

Date: 2026-06-04
Scope: quick security review and safe hardening of the current OnlineExam codebase (backend only).

High-level summary
- No intentionally malicious demos were added. The ElevateBootcamp paste was used only as an example. A concise project-specific vulnerability report follows and safe fixes were applied where gaps existed.

Findings (high-level)
1. Input validation (Accounts.Login): weak/missing DTO validation allowed empty/malformed credentials and risked BCrypt DoS. Severity: Medium.
2. File upload (Categories icons): client filename and weak validation risk path traversal, content spoofing, and DoS. Severity: High.
3. XSS (frontend announcements): potential if client bypasses Angular sanitization. Severity: High (if present in frontend). No server-side sanitization found for rich HTML.
4. SQL Injection: codebase predominantly uses EF Core LINQ; no unsafe raw SQL concatenation found in main repo. Severity: Low.
5. Unsafe native code / buffer overflow: not applicable — project is managed C# without AllowUnsafeBlocks.
6. Third-party packages: NuGet warnings for AutoMapper, MailKit, MimeKit with known advisories. Severity: Medium.

Actions performed (safe hardening)
- Added DTO validation for login (Features/Accounts/Dtos/LoginReqDTO.cs).
- Added request validation at endpoint level (Features/Accounts/Endpoints/LoginEndpoint.cs).
- Hardened login handler with server-side checks and length caps (Features/Accounts/Commands/LoginCommand.cs).
- Implemented FileUploadSecurityHelper (Shared/Helpers/FileUploadSecurityHelper.cs) and integrated it into categories create/update handlers (Features/Categories/Handelrs/*). This enforces extension allowlist, signature checks, safe filenames, path canonicalization, and size limits.
- Created `VULN_DEMO_GUIDE.md` at repo root (example content copied earlier as requested) and this project-specific `VULN_REPORT.md`.
- Verified solution builds successfully (dotnet build OnlineExam.sln).

Recommendations (prioritized)
- Update vulnerable NuGet packages or apply mitigations (AutoMapper, MailKit, MimeKit).
- Add unit/integration tests for upload validation and auth endpoints.
- Introduce SAST/SCA in CI (e.g., GitHub Actions: CodeQL, Dependabot alerts enabled).
- Add server-side HTML sanitization (HtmlSanitizer) if rich HTML is allowed; forbid bypassing Angular sanitizer in frontend.
- Add rate-limiting and request body size caps for auth endpoints to reduce DoS risk.
- Rotate any secrets if appsettings were shared during testing.

Next steps I can take
- Open a branch, commit these changes, and push a PR with a short summary.
- Add tests that assert upload rejection for non-image files and oversized payloads.
- Add a GitHub Actions workflow for CodeQL and Dependabot config.

Tell me which of the next steps to do (I can commit and open a PR now).