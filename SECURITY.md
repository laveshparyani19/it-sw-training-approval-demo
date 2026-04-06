# Security Guidelines

This document outlines security best practices and architectural decisions for the Approval Demo application.

## Environment Variables (NEVER commit to GitHub)

All sensitive data is managed via environment variables. For Render, set these in the dashboard:

- `SUPABASE_PASSWORD`: Postgres password (never hardcoded)

For local development, create `.env` (ignored by git):
```
SUPABASE_PASSWORD=your_password_here
```

## Input Validation

All DTOs are protected with `[Required]` and `[StringLength]` attributes:
- **Title**: 3-500 characters
- **RequestedBy**: 2-200 characters
- **DecisionBy**: 2-200 characters
- **RejectReason**: 0-1000 characters

The controller validates `ModelState` before processing requests.

## Database Security

- **Connection String**: Built at runtime from environment variable
- **SSL/TLS**: Required (`SSL Mode=Require`)
- **Parameterized Queries**: Used via Npgsql ORM (prevent SQL injection)
- **No stored procedures**: Plain SQL queries with parameters

## API Security

### CORS Policy
- Whitelist only trusted frontend domains
- Credentials allowed: Yes
- Methods allowed: POST, GET

### Request Size Limits
- Max payload: 100 KB
- Prevents memory exhaustion attacks

### HTTP Security Headers
- `X-Content-Type-Options: nosniff` - Prevent MIME type sniffing
- `X-Frame-Options: DENY` - Prevent clickjacking
- `X-XSS-Protection: 1; mode=block` - XSS protection
- `Content-Security-Policy` - Limit script execution
- `Referrer-Policy: strict-origin-when-cross-origin` - Control referrer leaks

### HTTPS Enforcement
- HSTS headers in production
- All traffic redirected to HTTPS

## Frontend Security (Angular)

- **No hardcoded secrets**: API URL is safe to expose
- **Supabase keys**: Not used in frontend (database accessed via backend only)
- **XSS Protection**: Angular sanitizes user input by default

## Logging

- ✅ **Production**: Only essential info logged (no debug statements)
- ✅ **Development**: Debug logs enabled for troubleshooting
- ⚠️ **Never log**: Passwords, tokens, PII

## What's NOT Implemented (Add Later for Production)

- [ ] Authentication/Authorization (JWT or OAuth)
- [ ] Rate limiting per IP/user
- [ ] API key management
- [ ] Database encryption at rest
- [ ] Audit logging (who did what, when)
- [ ] Secrets rotation policy
- [ ] Security scanning in CI/CD

## Security Checklist

Before deploying to production:

- [ ] All environment variables set securely
- [ ] HTTPS enforced (done)
- [ ] CORS configured correctly (done)
- [ ] No secrets in code or .gitignore (done)
- [ ] Input validation active (done)
- [ ] Security headers enabled (done)
- [ ] Swagger disabled in production (done)
- [ ] Request size limits enforced (done)
- [ ] Logging doesn't expose secrets (done)
- [ ] Dependencies up-to-date (check regularly)

## Reporting Security Issues

If you find a vulnerability, **do not open a GitHub issue**. Instead:
1. Contact `lavesh.paryani@protego.services` privately
2. Include proof-of-concept and impact assessment
3. Allow 48 hours for a response
