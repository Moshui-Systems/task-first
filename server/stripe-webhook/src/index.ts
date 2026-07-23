// TaskFirst licensing Worker
// ---------------------------
// Routes:
//   POST /stripe        Stripe webhook (checkout.session.completed) -> mint + email a key
//   POST /activate      register a device (enforces per-license activation limit)
//   POST /deactivate    release a device slot
//   GET  /revocations   { revoked: [licenseId, ...] }
//   POST /admin/revoke  { licenseId }   (header x-admin-token)
//   POST /admin/unrevoke{ licenseId }   (header x-admin-token)
//   GET  /              health check
//
// Secrets (wrangler secret put ...): STRIPE_WEBHOOK_SECRET, PRIVATE_KEY, PUBLIC_KEY,
//                                    RESEND_API_KEY, ADMIN_TOKEN
// Vars (wrangler.toml): ACTIVATION_LIMIT, EMAIL_FROM, PRODUCT_NAME, PRODUCT_ID

import {
  importPrivateKey,
  importPublicKey,
  signLicense,
  verifyToken,
  newLicenseId,
  type LicensePayload,
} from "./license";

export interface Env {
  KV: KVNamespace;
  STRIPE_WEBHOOK_SECRET: string;
  PRIVATE_KEY: string;
  PUBLIC_KEY: string;
  RESEND_API_KEY: string;
  ADMIN_TOKEN: string;
  ACTIVATION_LIMIT: string;
  EMAIL_FROM: string;
  PRODUCT_NAME: string;
  PRODUCT_ID: string;
}

interface LicenseRecord {
  email: string;
  key: string;
  createdAt: string;
  machines: string[];
}

const json = (data: unknown, status = 200) =>
  new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json" } });

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname.replace(/\/+$/, "") || "/";

    try {
      if (request.method === "POST" && path === "/stripe") return await handleStripe(request, env);
      if (request.method === "POST" && path === "/activate") return await handleActivate(request, env);
      if (request.method === "POST" && path === "/deactivate") return await handleDeactivate(request, env);
      if (request.method === "GET" && path === "/revocations") return await handleRevocations(env);
      if (request.method === "POST" && path === "/admin/revoke") return await handleRevoke(request, env, true);
      if (request.method === "POST" && path === "/admin/unrevoke") return await handleRevoke(request, env, false);
      if (request.method === "GET" && path === "/") return new Response("TaskFirst licensing OK", { status: 200 });
    } catch (err) {
      return json({ error: String(err) }, 500);
    }
    return json({ error: "not found" }, 404);
  },
};

// ---------- Stripe webhook ----------

async function handleStripe(request: Request, env: Env): Promise<Response> {
  const raw = await request.text();
  const sig = request.headers.get("stripe-signature") ?? "";

  if (!(await verifyStripeSignature(raw, sig, env.STRIPE_WEBHOOK_SECRET))) {
    return json({ error: "bad signature" }, 400);
  }

  const event = JSON.parse(raw);
  if (event.type !== "checkout.session.completed") return json({ received: true });

  // Idempotency: Stripe retries webhooks.
  const evtKey = `evt:${event.id}`;
  if (await env.KV.get(evtKey)) return json({ received: true, duplicate: true });

  const session = event.data.object;
  const email: string | undefined = session.customer_details?.email ?? session.customer_email;
  if (!email) return json({ error: "no email on session" }, 400);

  const days = parseInt(session.metadata?.license_days ?? "", 10);
  const token = await mintKey(env, email, Number.isFinite(days) && days > 0 ? days : undefined);

  await emailKey(env, email, token);
  await env.KV.put(evtKey, "1", { expirationTtl: 60 * 60 * 24 * 30 });

  return json({ received: true, issued: true });
}

async function mintKey(env: Env, email: string, days?: number): Promise<string> {
  const licenseId = newLicenseId();
  const payload: LicensePayload = {
    licenseId,
    email,
    product: env.PRODUCT_ID || "taskfirst",
    tier: "pro",
    features: [],
    issuedUtc: new Date().toISOString(),
  };
  if (days) payload.expiresUtc = new Date(Date.now() + days * 86400_000).toISOString();

  const priv = await importPrivateKey(env.PRIVATE_KEY);
  const token = await signLicense(payload, priv);

  const record: LicenseRecord = { email, key: token, createdAt: payload.issuedUtc, machines: [] };
  await env.KV.put(`lic:${licenseId}`, JSON.stringify(record));
  return token;
}

async function emailKey(env: Env, to: string, token: string): Promise<void> {
  if (!env.RESEND_API_KEY) return; // no email provider configured; key is still in KV
  const product = env.PRODUCT_NAME || "TaskFirst Pro";
  const body = {
    from: env.EMAIL_FROM,
    to,
    subject: `Your ${product} license key`,
    text:
      `Thanks for buying ${product}!\n\n` +
      `Open TaskFirst → "Upgrade to Pro", paste this key, and click Activate:\n\n${token}\n\n` +
      `Keep this email — it's your receipt and license.`,
  };
  await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${env.RESEND_API_KEY}`,
      "content-type": "application/json",
    },
    body: JSON.stringify(body),
  });
}

// ---------- activation ----------

async function handleActivate(request: Request, env: Env): Promise<Response> {
  const { licenseId, key, machineId } = await request.json<any>();
  if (!licenseId || !key || !machineId) return json({ status: "invalid" });

  const pub = await importPublicKey(env.PUBLIC_KEY);
  const v = await verifyToken(key, pub);
  if (!v.ok || v.payload?.licenseId !== licenseId) return json({ status: "invalid" });

  if (await isRevoked(env, licenseId)) return json({ status: "revoked" });

  const limit = parseInt(env.ACTIVATION_LIMIT || "3", 10);
  const recKey = `lic:${licenseId}`;
  const rec: LicenseRecord =
    (await env.KV.get<LicenseRecord>(recKey, "json")) ??
    { email: v.payload!.email, key, createdAt: new Date().toISOString(), machines: [] };

  if (!rec.machines.includes(machineId)) {
    if (rec.machines.length >= limit) {
      return json({ status: "limit_reached", activations: rec.machines.length, limit });
    }
    rec.machines.push(machineId);
    await env.KV.put(recKey, JSON.stringify(rec));
  }

  return json({ status: "ok", activations: rec.machines.length, limit });
}

async function handleDeactivate(request: Request, env: Env): Promise<Response> {
  const { licenseId, machineId } = await request.json<any>();
  const recKey = `lic:${licenseId}`;
  const rec = await env.KV.get<LicenseRecord>(recKey, "json");
  if (rec) {
    rec.machines = rec.machines.filter((m) => m !== machineId);
    await env.KV.put(recKey, JSON.stringify(rec));
  }
  return json({ status: "ok" });
}

// ---------- revocation ----------

async function revokedList(env: Env): Promise<string[]> {
  return (await env.KV.get<string[]>("revoked", "json")) ?? [];
}

async function isRevoked(env: Env, licenseId: string): Promise<boolean> {
  return (await revokedList(env)).includes(licenseId);
}

async function handleRevocations(env: Env): Promise<Response> {
  return json({ revoked: await revokedList(env) });
}

async function handleRevoke(request: Request, env: Env, add: boolean): Promise<Response> {
  if (request.headers.get("x-admin-token") !== env.ADMIN_TOKEN) return json({ error: "unauthorized" }, 401);
  const { licenseId } = await request.json<any>();
  if (!licenseId) return json({ error: "licenseId required" }, 400);

  let list = await revokedList(env);
  list = add ? [...new Set([...list, licenseId])] : list.filter((x) => x !== licenseId);
  await env.KV.put("revoked", JSON.stringify(list));
  return json({ revoked: list });
}

// ---------- Stripe signature (manual HMAC, no SDK) ----------

async function verifyStripeSignature(payload: string, header: string, secret: string): Promise<boolean> {
  if (!secret || !header) return false;
  const parts = Object.fromEntries(header.split(",").map((p) => p.split("=") as [string, string]));
  const t = parts["t"];
  const v1 = parts["v1"];
  if (!t || !v1) return false;

  // Reject events older than 5 minutes (replay protection).
  if (Math.abs(Date.now() / 1000 - Number(t)) > 300) return false;

  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const mac = new Uint8Array(
    await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(`${t}.${payload}`)),
  );
  const expected = [...mac].map((b) => b.toString(16).padStart(2, "0")).join("");
  return timingSafeEqual(expected, v1);
}

function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}
