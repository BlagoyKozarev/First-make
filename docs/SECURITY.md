# Security Documentation

**FirstMake Agent - Security Guide**  
Version: 1.0.0  
Last Updated: October 28, 2025

---

## 🔐 Secret Scan Results

### Executive Summary

**Scan Date:** October 28, 2025  
**Tool Used:** detect-secrets v1.5.0  
**Status:** ⚠️ **CRITICAL FINDING - Leaked Credentials Detected**

### Critical Finding

**File:** `src/AiGateway/appsettings.json`  
**Issue:** Real API credentials committed to git repository  
**Risk Level:** 🔴 **CRITICAL**

```json
"BgGpt": {
  "ApiUrl": "http://api.raicommerce.net/v1/completions",
  "Username": "Raicommerce",
  "Password": "R@icommerce23",
  "ApiKey": "R@icommerce23",
  ...
}
```

**Impact:**
- ✅ Credentials are in **public GitHub repository**
- ✅ Exposed in commit `b1518d3` (tag v1.0.0)
- ✅ Available in git history (16 total commits)
- ⚠️ Potential unauthorized access to api.raicommerce.net

### Non-Critical Findings

All other findings are **FALSE POSITIVES**:

1. **Documentation placeholders** in README.md:
   - `"YOUR_API_KEY"` - example text
   - `"your-api-key-here"` - example text

2. **Build artifacts** (not in source control, auto-generated):
   - `*.deps.json` files (NuGet package hashes)
   - `project.assets.json` files (MSBuild hashes)
   - `node_modules/` files (npm package metadata)

3. **Test fixtures** in node_modules:
   - Password validation test data
   - Mock credentials for unit tests

---

## 🛡️ Immediate Actions Required

### 1. Revoke Compromised Credentials

**URGENT:** Contact api.raicommerce.net administrator to:
- [ ] Rotate `Username: Raicommerce`
- [ ] Rotate `Password: R@icommerce23`
- [ ] Rotate `ApiKey: R@icommerce23`
- [ ] Review access logs for unauthorized usage
- [ ] Implement new credentials with stronger password policy

### 2. Remove Credentials from Git History

**Option A: Rewrite History** (if no critical forks exist)
```bash
# Install git-filter-repo
pip3 install git-filter-repo

# Create backup
git clone --mirror https://github.com/GitRaicommerce/First-make.git backup

# Remove sensitive file from history
cd /workspaces/First-make
git filter-repo --path src/AiGateway/appsettings.json --invert-paths

# Force push (CAUTION: rewrites history)
git push origin --force --all
git push origin --force --tags
```

**Option B: Use BFG Repo-Cleaner** (alternative)
```bash
# Install BFG
wget https://repo1.maven.org/maven2/com/madgag/bfg/1.14.0/bfg-1.14.0.jar

# Replace passwords in history
java -jar bfg-1.14.0.jar --replace-text passwords.txt First-make.git

# Clean up
cd First-make.git
git reflog expire --expire=now --all && git gc --prune=now --aggressive
git push --force
```

**Option C: Accept Risk** (if history rewrite impossible)
- Document that old credentials are revoked
- Ensure new credentials are used
- Add note in SECURITY.md about historical leak

### 3. Implement Secrets Management

**Create Template File:**
```bash
# Create appsettings.json template
cat > src/AiGateway/appsettings.template.json <<'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "BgGpt": {
    "ApiUrl": "http://api.raicommerce.net/v1/completions",
    "Username": "${BGGPT_USERNAME}",
    "Password": "${BGGPT_PASSWORD}",
    "ApiKey": "${BGGPT_API_KEY}",
    "Model": "bg-gpt-7b",
    "Temperature": 0.1,
    "MaxTokens": 4096,
    "TimeoutSeconds": 60
  },
  "Tesseract": {
    "ExecutablePath": "/usr/bin/tesseract",
    "Languages": "bul+eng",
    "Psm": 6,
    "TimeoutSeconds": 30
  },
  "Schemas": {
    "BoqSchemaPath": "/workspaces/First-make/Schemas/boq.schema.json",
    "PlaybookPath": "/workspaces/First-make/Schemas/playbooks/extract.yaml",
    "SystemPromptPath": "/workspaces/First-make/Schemas/prompts/extract.system.txt",
    "UserPromptPath": "/workspaces/First-make/Schemas/prompts/extract.user.txt"
  }
}
EOF
```

**Update .gitignore:**
```bash
# Add to .gitignore
echo "" >> .gitignore
echo "# Secrets" >> .gitignore
echo "src/AiGateway/appsettings.json" >> .gitignore
echo "src/AiGateway/appsettings.*.json" >> .gitignore
echo "!src/AiGateway/appsettings.template.json" >> .gitignore
```

**Use Environment Variables:**
```bash
# Production deployment
export BGGPT_USERNAME="new-username"
export BGGPT_PASSWORD="new-strong-password-here"
export BGGPT_API_KEY="new-api-key-here"
```

---

## 🔧 GitHub Actions Secrets Setup

### Adding Secrets to GitHub

1. Navigate to: `https://github.com/GitRaicommerce/First-make/settings/secrets/actions`

2. Click **"New repository secret"**

3. Add required secrets:

| Secret Name | Description | Example |
|------------|-------------|---------|
| `BGGPT_API_KEY` | BgGpt API authentication key | `sk-prod-abc123...` |
| `BGGPT_USERNAME` | BgGpt service username | `FirstMakeBot` |
| `BGGPT_PASSWORD` | BgGpt service password | `$tr0ng!P@ssw0rd` |
| `DOCKER_USERNAME` | GitHub username for GHCR | `GitRaicommerce` |
| `DOCKER_TOKEN` | GitHub PAT for package push | `ghp_abc123...` |

### Using Secrets in Workflows

```yaml
name: Build and Publish

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Build Docker Image
        run: |
          docker build \
            --build-arg BGGPT_USERNAME=${{ secrets.BGGPT_USERNAME }} \
            --build-arg BGGPT_PASSWORD=${{ secrets.BGGPT_PASSWORD }} \
            --build-arg BGGPT_API_KEY=${{ secrets.BGGPT_API_KEY }} \
            -t ghcr.io/gitraicommerce/firstmake:${{ github.ref_name }} .
      
      - name: Push to GHCR
        run: |
          echo ${{ secrets.DOCKER_TOKEN }} | docker login ghcr.io -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
          docker push ghcr.io/gitraicommerce/firstmake:${{ github.ref_name }}
```

---

## 🔍 Ongoing Secret Scanning

### Pre-commit Hook Setup

Install pre-commit hook to catch secrets before commit:

```bash
# Install detect-secrets
pip3 install detect-secrets

# Generate baseline
detect-secrets scan > .secrets.baseline

# Add pre-commit hook
cat > .git/hooks/pre-commit <<'EOF'
#!/bin/bash
detect-secrets scan --baseline .secrets.baseline
if [ $? -ne 0 ]; then
  echo "❌ Potential secrets detected! Commit aborted."
  exit 1
fi
EOF

chmod +x .git/hooks/pre-commit
```

### GitHub Actions Secret Scanning

Add to `.github/workflows/security-scan.yml`:

```yaml
name: Security Scan

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  secret-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Full history for deep scan
      
      - name: Install detect-secrets
        run: pip3 install detect-secrets
      
      - name: Scan for secrets
        run: |
          detect-secrets scan --all-files > scan-results.json
          if grep -q '"results":' scan-results.json; then
            echo "❌ Secrets detected!"
            cat scan-results.json
            exit 1
          fi
          echo "✅ No secrets found"
```

### Periodic Audits

Schedule monthly security audits:

```bash
# Add to crontab
0 0 1 * * cd /workspaces/First-make && detect-secrets scan --all-files | mail -s "FirstMake Secret Scan" admin@example.com
```

---

## 📋 Security Checklist

### Before Every Commit
- [ ] Run `detect-secrets scan` on changed files
- [ ] Review git diff for hardcoded credentials
- [ ] Ensure `.env` files are in `.gitignore`
- [ ] Use environment variables for sensitive data

### Before Every Deployment
- [ ] Rotate production API keys
- [ ] Update GitHub Actions secrets
- [ ] Review docker-compose.yml for exposed ports
- [ ] Enable SSL/TLS for all public endpoints
- [ ] Configure firewall rules
- [ ] Enable rate limiting on APIs

### Monthly
- [ ] Run full repository secret scan
- [ ] Review access logs for anomalies
- [ ] Update dependencies (npm audit, dotnet list package --vulnerable)
- [ ] Rotate database passwords
- [ ] Review GitHub Actions logs

### Quarterly
- [ ] Penetration testing
- [ ] Dependency security audit
- [ ] Review user permissions
- [ ] Update security documentation

---

## 🚨 Incident Response

### If Credentials Are Leaked

1. **Immediate Actions** (within 1 hour):
   - Revoke compromised credentials
   - Generate new credentials
   - Update production deployments
   - Monitor for unauthorized access

2. **Investigation** (within 24 hours):
   - Review access logs
   - Identify scope of exposure
   - Check for data exfiltration
   - Document timeline

3. **Remediation** (within 1 week):
   - Remove credentials from git history
   - Implement secrets management
   - Add pre-commit hooks
   - Update documentation

4. **Post-Mortem** (within 2 weeks):
   - Root cause analysis
   - Process improvements
   - Team training
   - Update incident playbook

---

## 🔗 Resources

- [GitHub Secrets Management](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [detect-secrets Documentation](https://github.com/Yelp/detect-secrets)
- [git-filter-repo](https://github.com/newren/git-filter-repo)
- [BFG Repo-Cleaner](https://rtyley.github.io/bfg-repo-cleaner/)

---

## 📞 Contact

**Security Issues:** Create private security advisory at:  
`https://github.com/GitRaicommerce/First-make/security/advisories`

**Urgent Security Contact:**  
Email: security@raicommerce.net  
Response Time: < 4 hours

---

*This document was generated as part of the v1.0.0 security audit.*
