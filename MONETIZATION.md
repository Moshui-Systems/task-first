# Selling TaskFirst Pro

TaskFirst uses **offline, cryptographically-signed license keys** (ECDSA P-256). You hold a
private key; the app ships only the matching public key and verifies purchases with **no server
call**. That keeps hosting cost at zero and works offline for the customer.

```
buyer pays  ─▶  you (or a webhook) mint a signed key  ─▶  buyer pastes it into the app
                     licensetool issue <email>                app verifies vs embedded public key
```

## Tiers

| | Free | Pro |
|---|---|---|
| Minimize-not-kill blocking | ✅ | ✅ |
| Anki unlock gate | ✅ | ✅ |
| Pomodoro widget | ✅ | ✅ |
| Blocking rules | 2 | Unlimited |
| Schedules (hours/days) | — | ✅ |
| Tamper-lock (password to disable/quit) | — | ✅ |
| Priority support / future Pro gates | — | ✅ |

Suggested price: **$19 one-time (perpetual)** or **$12/yr**. Perpetual keys are the default;
pass `--days 365` to `issue` for a subscription-style expiring key.

## One-time setup

1. **Generate your keypair** (already done once for this repo; redo only to rotate):
   ```powershell
   dotnet run --project tools/LicenseTool -- keygen
   ```
   - Paste the **public** key into `Licensing/Entitlements.cs` → `PublicKeyBase64`.
   - Store the **private** key somewhere safe and secret. It is already in
     `%USERPROFILE%\.taskfirst-keys\private.key` and in the `TASKFIRST_PRIVATE_KEY` env var on this
     machine. **Never commit it** (`.gitignore` blocks `*.key`).
2. Set your checkout URL in `Entitlements.cs` → `BuyUrl`.

## Minting a key for a buyer

```powershell
# perpetual
dotnet run --project tools/LicenseTool -- issue buyer@example.com --key-file "$env:USERPROFILE\.taskfirst-keys\private.key"

# 1-year subscription
dotnet run --project tools/LicenseTool -- issue buyer@example.com --days 365
```

Copy the printed **LICENSE KEY** to the buyer. They open **Upgrade to Pro** in the app, paste it,
click **Activate**. Verify any key with:

```powershell
dotnet run --project tools/LicenseTool -- verify <key>
```

## Taking payments — Stripe (chosen) with manual fulfilment

Plan: **Stripe Payment Link + manual key delivery** to launch today, automate later.

### Launch checklist (manual fulfilment)
1. **Stripe Dashboard → Product catalog → Add product**: "TaskFirst Pro", one-time price (e.g. $19).
2. **Payment Links → New link** for that price. A Payment Link **collects the buyer's email by
   default** — that's the email you'll bind the key to. Copy the `https://buy.stripe.com/…` URL.
3. Paste it into `Licensing/Entitlements.cs` → `BuyUrl`, rebuild/release.
4. Turn on a notification for new payments (Stripe app or the "successful payment" email).
5. **On each sale:** grab the buyer's email from the payment, then mint and send the key:
   ```powershell
   dotnet run --project tools/LicenseTool -- issue buyer@example.com `
       --key-file "$env:USERPROFILE\.taskfirst-keys\private.key"
   ```
   Email them the key (a saved template works fine). Buyer pastes it into **Upgrade to Pro**.

> Tip: keep a simple spreadsheet (date, email, license id) so support/refunds are easy. The
> `issue` command prints a `License id` for exactly this.

### Tax note
Stripe is **not** a merchant of record — you're responsible for tax. Turn on **Stripe Tax** so it
calculates VAT/sales tax at checkout, and understand your registration thresholds. (If that
overhead ever outweighs the lower fees, Lemon Squeezy/Paddle act as merchant of record and remit
tax for you — the app's `BuyUrl` is the only thing that would change.)

### Automating later (when volume justifies it)
Replace the manual step with a webhook that holds the private key and mints on purchase:
1. Deploy a serverless function (Cloudflare Workers / Vercel / Azure Function). Port
   `LicenseToken.Sign` (≈40 lines) or shell out to `licensetool`.
2. Store the private key as a secret env var there (never in the repo).
3. Point Stripe's **`checkout.session.completed`** webhook at it; mint a key for
   `session.customer_details.email` and email it (Resend/SendGrid). Verify the Stripe
   webhook signature before minting.

## Anti-piracy note

Offline keys can be shared. This is deliberate friction, not DRM. To tighten later:
- Bind a key to a machine id on first activation (add a `MachineId` claim, or store an activation
  record via the store's activation-limit API).
- Publish a short **revocation list** the app fetches occasionally to kill leaked keys.
- Keep expiring (subscription) keys for the strictest control.

Don't over-invest here — for a focus tool, honest customers are the market.
