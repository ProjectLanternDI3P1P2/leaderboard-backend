# Architecture Decision Record (ADR): Progression Service Dockerfile Hardening

**Scope:** Progression / Leaderboard Microservice  
**Topic:** Container Image Security and Build Optimization  
**Role:** Ops / Platform Engineering  

---

## 1. Before: The Initial State

The initial Dockerfile was functional but not optimized for a production Kubernetes environment. It presented several security and operational risks:

* **Large Attack Surface:** Relied on the standard ASP.NET runtime image, which includes a full shell (`/bin/sh`), package managers (`apt`), and system utilities.
* **Exposed Private Network Surface:** Exposed both port 8080 (HTTP) and 8081 (HTTPS), implying the container should handle TLS internally.
* **Information Leakage:** Compiled with full debug symbols (`.pdb` files) and kept .NET diagnostic endpoints active.
* **Inconsistent Permissions:** Copied compiled artifacts as the `root` user, even though the execution was instructed to run as `$APP_UID`.
* **Active Telemetry:** Microsoft's default .NET CLI telemetry remained enabled.

---

## 2. After: The Hardened State

The Dockerfile was rewritten to enforce a strict security baseline tailored for Kubernetes deployment:

* **Distroless Runtime:** Migrated to the `mcr.microsoft.com/dotnet/aspnet:10.0-chiseled` image.
* **Single Unprivileged Port:** Removed port 8081; the container now exclusively exposes port 8080 (HTTP).
* **Stripped Binaries:** Added `/p:DebugType=None` and `/p:DebugSymbols=false` to the build stage.
* **Disabled Diagnostics & Telemetry:** Injected `ENV DOTNET_EnableDiagnostics=0` and `DOTNET_CLI_TELEMETRY_OPTOUT=1` into the runtime.
* **Strict Ownership:** Implemented `COPY --chown=$APP_UID:$APP_UID` to align file ownership with the unprivileged runtime user.

---

## 3. Why: The Rationale (Alignment with Project Pillars)

These changes were made to align with the **Project's Pillar 5 (Security)** and **Pillar 3 (Platform Engineering)** constraints.

### A. Zero-Trust & Blast Radius Reduction (The "Chiseled" Choice)
By switching to a "chiseled" (distroless) image, we completely remove the OS shell and package manager. If an attacker finds a Remote Code Execution (RCE) vulnerability in our Leaderboard code, they cannot open a reverse shell, download malware via `curl`, or install new tools. The blast radius is strictly contained to the running .NET process.

### B. TLS Termination at the Ingress Level (Removing Port 8081)
Managing TLS certificates inside individual microservices is an anti-pattern that violates the Golden Path principle. By removing port 8081, we delegate TLS termination to the Kubernetes Ingress Controller (or Service Mesh). This ensures our pod never holds a private TLS key, eliminating the risk of certificate theft in case of a container breach.

### C. Preventing Memory Dumps & Reverse Engineering
Disabling `DOTNET_EnableDiagnostics` shuts down the .NET IPC channels used for remote profiling and memory dumping. This prevents an attacker (or a compromised sidecar) from extracting sensitive data or injecting malicious profilers into the running process. Stripping debug symbols (`DebugType=None`) keeps the image lightweight and hides internal application structures.

### D. Principle of Least Privilege
While the original file had `USER $APP_UID`, the files copied from the build stage were still owned by `root`. Using `--chown=$APP_UID:$APP_UID` ensures the application user legitimately owns its working directory, preventing permission conflicts and enforcing true non-root execution.
