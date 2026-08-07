# TaskFirst launch runbook

Two tracks. **Track A** puts a real, downloadable product + website live today (no payment infra).
**Track B** turns on paid Pro. Do A first — it's a legit launch on its own.

---

## Track A — Free launch (website + downloadable app)

### A1. Turn on the website (2 min)
The app + releases already work; only Pages is off (that's why the `pages` workflow failed).

1. GitHub → repo **Settings → Pages**.
2. **Build and deployment → Source → GitHub Actions**.
3. Re-run the failed run: **Actions → pages → Re-run all jobs** (or just push any docs change).
4. Site goes live at **https://moshui-systems.github.io/task-first/**.

### A2. Sanity-check the download
- From the site, **Download free** → gets the latest release zip.
- Unzip → run `TaskFirst.exe`. Expect a **UAC prompt** (it's admin-by-default) → tray icon "TaskFirst (admin)".

### A3. Code-signing (do soon, not blocking)
Unsigned exes trigger SmartScreen ("Windows protected your PC"), which kills conversion.
- Cheapest real fix: an **OV code-signing certificate** (~$70–200/yr, e.g. Certera/SSL.com) or
  **Azure Trusted Signing** (~$10/mo, easiest if you'll sign regularly).
- Until then, add a short "click More info → Run anyway" note on the download page.

---

## Track B — Turn on paid Pro

Order matters: deploy the Worker → wire Stripe → set the two URLs in the app → re-release → test.

### B1. Resend (email delivery) — ~10 min
1. Create a [Resend](https://resend.com) account, **add your sending domain**, add the DNS records.
2. Create an **API key** → you'll set it as the `RESEND_API_KEY` Worker secret.
3. Set `EMAIL_FROM` in `server/stripe-webhook/wrangler.toml` to a `you@yourdomain` address.

### B2. Deploy the Cloudflare Worker — ~15 min
From `server/stripe-webhook/` (full detail in its README):
```bash
npm install
npx wrangler login
npx wrangler kv namespace create KV        # paste id into wrangler.toml
npx wrangler secret put PRIVATE_KEY        # from ~/.taskfirst-keys/private.key
npx wrangler secret put PUBLIC_KEY         # the key embedded in Entitlements.cs
npx wrangler secret put STRIPE_WEBHOOK_SECRET   # from B3
npx wrangler secret put RESEND_API_KEY
npx wrangler secret put ADMIN_TOKEN        # any long random string
npx wrangler deploy
```
→ note the URL, e.g. `https://taskfirst-licensing.<you>.workers.dev`.

### B3. Stripe — ~15 min
1. **Products** → create "TaskFirst Pro", one-time price **$19**.
2. **Payment Links** → new link for that price → copy the `https://buy.stripe.com/…` URL.
3. **Developers → Webhooks → Add endpoint**:
   `https://taskfirst-licensing.<you>.workers.dev/stripe`, event `checkout.session.completed`.
   Copy the **Signing secret** (`whsec_…`) → that's `STRIPE_WEBHOOK_SECRET` in B2.
4. **Settings → Tax** → enable **Stripe Tax** (you're the merchant of record here).

### B4. Point the app at your live services
Edit `Licensing/Entitlements.cs`:
- `BuyUrl`            = the Stripe Payment Link from B3.
- `LicensingApiBase`  = the Worker URL from B2.

Also update the two `https://buy.stripe.com/REPLACE_ME` links in `docs/index.html`.

### B5. Ship it
```bash
git commit -am "Wire live Stripe + licensing Worker URLs"
git push
git tag v0.4.0 && git push origin v0.4.0     # builds the release with live links
```

### B6. Test the whole loop (Stripe test mode first)
1. Stripe **test mode** → use the test Payment Link + card `4242 4242 4242 4242`.
2. Confirm: webhook fires → you receive the license email → paste key in app → **Pro** activates.
3. Try activating on a 4th device → expect "activation limit reached".
4. `POST /admin/revoke` the test license → app drops to Free on next launch.
5. Flip Stripe to **live mode**, swap the live Payment Link into `BuyUrl`, re-tag.

---

## Launch-day checklist (once A+B are green)
- [ ] Website live, download works, buy button works (test purchase refunded).
- [ ] A 20-second demo GIF on the landing page + README (blocked app bounces → Anki gate → unlock).
- [ ] Post where your users are: r/Anki, r/medicalschool, r/productivity, Product Hunt, X.
- [ ] A support email on the site + in the license email.
