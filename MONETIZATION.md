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

## Taking payments — recommended: Lemon Squeezy or Paddle

Both are **merchants of record**, meaning they collect and remit VAT/sales tax for you — a big deal
for a solo dev selling worldwide. (Gumroad is the simplest but you handle more; Stripe is cheapest
but you own tax compliance.)

### Fastest launch (manual fulfilment)
1. Create a product on **Lemon Squeezy**, price it, get the checkout URL → put it in `BuyUrl`.
2. When you get the "new order" email, run `issue <buyer-email>` and send the key. Lemon Squeezy
   lets you customize the order-confirmation email, so you can paste the key there.

### Automated fulfilment (recommended once you have volume)
Host a tiny webhook that holds the private key and mints on purchase:
1. Deploy a serverless function (Cloudflare Workers / Vercel / a small Azure Function). Port the
   `LicenseToken.Sign` logic (≈40 lines) or shell out to `licensetool`.
2. Store the private key as a secret env var in that platform (never in the repo).
3. Point the store's **`order_created`** webhook at it. On each order, mint a key for the buyer's
   email and email it back (Resend/SendGrid) or return it via the store's license-key field.

### Alternative: use the store's own license keys
Lemon Squeezy and Gumroad can generate & validate their own keys via an API. If you prefer that,
add a `LemonSqueezyVerifier` alongside the current offline verifier and call their
`/v1/licenses/validate` endpoint (online check, cache the result). The offline signed-key path
stays as the fallback / enterprise option.

## Anti-piracy note

Offline keys can be shared. This is deliberate friction, not DRM. To tighten later:
- Bind a key to a machine id on first activation (add a `MachineId` claim, or store an activation
  record via the store's activation-limit API).
- Publish a short **revocation list** the app fetches occasionally to kill leaked keys.
- Keep expiring (subscription) keys for the strictest control.

Don't over-invest here — for a focus tool, honest customers are the market.
