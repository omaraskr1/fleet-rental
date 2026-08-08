# EventDrive — Production Cost Plan

Estimated monthly cost to run EventDrive for real customers, based on the actual stack in this repo:
ASP.NET Core API + SQL Server (`docker-compose.yml`), an Angular/Ionic web/PWA frontend, SMTP email
(`EmailNotificationSender`), and a push-notification path that is fully built except for the final
FCM/APNs call (`PushNotificationSender`).

Prices are August 2026 list prices in USD, rounded. Actual bills vary by region and usage — treat these
as planning numbers, not quotes. Re-check current pricing before committing to a vendor.

---

## 1. One-time / setup costs

| Item | Cost | Notes |
|---|---|---|
| Domain name (.com) | $10–15/yr | Namecheap/Cloudflare Registrar. Renews yearly. |
| SSL certificate | $0 | Free via Let's Encrypt or included automatically with most PaaS hosts (Azure App Service, Render, Fly.io all provision this for you). No reason to pay for a cert in 2026. |
| Apple Developer account (if publishing an iOS app later) | $99/yr | Only needed once the mobile app ships to the App Store. Not required for the web/PWA client. |
| Google Play Developer account (if publishing an Android app later) | $25 one-time | Same caveat — deferred per your instructions. |

Nothing here blocks launch; SSL and domain are the only two you need before going live.

---

## 2. Recurring monthly costs, by tier

### Tier A — "One real client" (low traffic, prove-it-works stage)

Enough to run one company's fleet with real users, without babysitting infrastructure.

| Item | Option | Cost/mo |
|---|---|---|
| API hosting | Azure App Service B1 (1 core/1.75GB) or Render/Fly.io equivalent | $13–15 |
| Database | Azure SQL Database, Basic/S0 tier (matches existing SQL Server target, no code changes) | $5–15 |
| Frontend hosting | Static hosting (Cloudflare Pages / Netlify / Vercel free tier, or same App Service as API) | $0 |
| Domain | amortized | ~$1 |
| SSL | Let's Encrypt / platform-managed | $0 |
| Email (transactional) | SendGrid/Postmark free or lowest paid tier (up to ~3–10k emails/mo) | $0–15 |
| Push notifications (FCM) | Firebase Cloud Messaging | $0 (FCM itself is free at any volume) |
| Backups | Included in most managed DB tiers, or a small blob-storage bucket for manual exports | $1–5 |
| Monitoring/logs (optional but recommended) | Azure Monitor free tier / free-tier Sentry or similar | $0 |
| **Total** | | **≈ $20–50/mo** |

This tier is a single small VM-class instance and a small managed database — fine for one company,
a handful of admins, and normal booking/maintenance traffic. It will not autoscale and has minimal
redundancy (a restart or deploy causes a brief blip), which is an acceptable tradeoff for an early
single-customer launch.

### Tier B — "Handles real multi-tenant traffic" (several paying companies, real SLAs)

What you'd move to once you have multiple companies depending on uptime, or traffic grows past what a
single small instance handles comfortably.

| Item | Option | Cost/mo |
|---|---|---|
| API hosting | Azure App Service S1/P1v3 (or 2+ instances behind a load balancer) with autoscale | $70–150 |
| Database | Azure SQL Database, Standard S3/S4 tier or a managed instance with point-in-time restore | $75–200 |
| Frontend hosting | CDN-backed static hosting (Cloudflare Pages Pro or equivalent) | $0–20 |
| Domain | amortized | ~$1 |
| SSL | platform-managed | $0 |
| Email (transactional) | SendGrid/Postmark paid tier (up to ~100k emails/mo) | $20–90 |
| Push notifications (FCM) | Firebase Cloud Messaging | $0 |
| Backups | Automated daily backups + geo-redundant storage | $10–30 |
| Monitoring/alerting | Application Insights or Sentry paid tier, uptime checks | $20–50 |
| Staging environment | A second, smaller copy of the above (App Service + DB, low tier) | $20–40 |
| **Total** | | **≈ $220–580/mo** |

This tier adds real headroom: autoscaling, a database tier with proper backup/restore SLAs, alerting
so you find out about outages before your customers do, and a staging slot so you stop testing changes
directly against production data (something to fix before the second real customer signs on).

---

## 3. Items intentionally NOT included here

- **Mobile app store hosting/CI** — no ongoing cost beyond the one-time developer accounts above;
  deferred per your instruction until app builds are actually in scope.
- **SMS notifications** — not currently implemented in the codebase (only email and push exist as
  channels); add ~$0.0075–0.01/message via Twilio if you decide you need it later.
- **A dedicated DevOps/SRE hire** — out of scope for a cost-of-infrastructure estimate, but worth
  flagging: at Tier B, someone needs to own on-call for the alerting you're now paying for.
- **Payment processing fees** (Stripe/PayPal etc., if you start charging companies or renters directly)
  — not infrastructure, and not yet wired into the app; typically ~2.9% + $0.30/transaction if added.

---

## 4. Bottom line

- **Launching with your one real customer:** roughly **$20–50/month**, and every piece of that
  (App Service, Azure SQL, SendGrid, FCM) is a direct drop-in for what's already in the codebase —
  no architecture changes needed, just provisioning real accounts to replace the Docker Compose test
  stack.
- **Once you're running several paying companies and need real uptime guarantees:** budget
  **$220–580/month**, mostly driven by a bigger database tier, redundant compute, and a staging
  environment.
- The jump between tiers is a config/scaling change, not a rewrite — the app already targets SQL
  Server and already has the email/push abstractions in place, so moving from Tier A to Tier B is
  "turn up the dial," not "re-architect."
