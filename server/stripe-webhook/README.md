# TaskFirst licensing Worker

A single Cloudflare Worker that automates the whole license lifecycle:

- **`POST /stripe`** — Stripe `checkout.session.completed` webhook → mints a signed key and emails it.
- **`POST /activate`** — registers a device; enforces a per-license activation limit (machine-binding).
- **`POST /deactivate`** — frees a device slot.
- **`GET /revocations`** — the revocation list the app checks on startup.
- **`POST /admin/revoke` / `/admin/unrevoke`** — kill/restore a leaked key (needs `x-admin-token`).

Keys are ECDSA P-256, signed with the **same private key** your `licensetool` uses and verified by
the app against the **same embedded public key** — the Worker just automates minting. It signs with
Web Crypto (raw r‖s / IEEE-P1363), which is exactly the format the C# app now verifies.

## Deploy

```bash
cd server/stripe-webhook
npm install
npx wrangler login

# 1) KV for license records + revocation list
npx wrangler kv namespace create KV          # paste the id into wrangler.toml

# 2) Secrets (never committed)
npx wrangler secret put PRIVATE_KEY          # from: dotnet run --project tools/LicenseTool -- keygen
npx wrangler secret put PUBLIC_KEY           # the same key embedded in Entitlements.cs
npx wrangler secret put STRIPE_WEBHOOK_SECRET
npx wrangler secret put RESEND_API_KEY       # https://resend.com (verify your sending domain)
npx wrangler secret put ADMIN_TOKEN          # any long random string

# 3) Ship it
npx wrangler deploy
```

You'll get a URL like `https://taskfirst-licensing.<you>.workers.dev`. Put it in the app at
`Licensing/Entitlements.cs` → `LicensingApiBase`, then cut a new release.

## Wire up Stripe

1. Stripe Dashboard → **Developers → Webhooks → Add endpoint**
   `https://taskfirst-licensing.<you>.workers.dev/stripe`, event **`checkout.session.completed`**.
2. Copy the endpoint's **Signing secret** (`whsec_…`) into `wrangler secret put STRIPE_WEBHOOK_SECRET`.
3. (Optional) On the Payment Link / Checkout Session set metadata `license_days=365` to issue a
   1-year key instead of perpetual.

Now a completed purchase auto-mints a key and emails it. The buyer pastes it into **Upgrade to Pro**;
the app calls `/activate` to bind their device.

## Revoke a leaked key

```bash
curl -X POST https://taskfirst-licensing.<you>.workers.dev/admin/revoke \
  -H "x-admin-token: <ADMIN_TOKEN>" -H "content-type: application/json" \
  -d '{"licenseId":"5807226be862"}'
```

The app drops to Free on its next revocation check.

## Local dev

```bash
cp .dev.vars.example .dev.vars   # fill in secrets
npm run dev
npm run typecheck
```

> Machine-binding is deliberately soft: if the Worker is unreachable at activation time, the app
> still activates offline and registers the device later. This protects paying customers from
> outages — it's friction against casual sharing, not hard DRM.
