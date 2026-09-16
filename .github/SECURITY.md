# Security Policy

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.**

If you believe you have found a security vulnerability in Soulsjwa, please report it
privately through GitHub's **Security Advisories** feature:

👉 <https://github.com/0xclx/Soulsjwa/security/advisories/new>

Alternatively, you can email the maintainers directly via the email address on the
repository owner's GitHub profile.

### What to include

When reporting, please include as much of the following information as you can:

- Type of vulnerability (e.g. XSS, SQL injection, authentication bypass, secret leak, …)
- Step-by-step instructions to reproduce the issue
- The affected version(s) / commit SHA
- Proof-of-concept code or a sample payload, if possible
- The potential impact (what an attacker could do)

### What to expect

- We will acknowledge your report within **3 working days**.
- We will provide an initial assessment of the vulnerability within **10 working days**.
- We will keep you informed of progress towards a fix and a public disclosure.
- We will credit you in the published advisory unless you ask to remain anonymous.

## Supported Versions

Only the latest commit on the `main` branch is supported. Security fixes are
not back-ported to tagged releases.

| Version | Supported          |
| ------- | ------------------ |
| `main`  | :white_check_mark: |
| < main  | :x:                |

## Security architecture

A high-level description of the project's security architecture (authentication,
authorisation, secret handling, rate limiting, security headers, etc.) lives in
[`docs/system-overview.md`](../docs/system-overview.md). The most recent
reviews, with what they found and what was done about it, are
[`docs/history/backend-review.md`](../docs/history/backend-review.md) and
[`docs/history/frontend-review.md`](../docs/history/frontend-review.md).
