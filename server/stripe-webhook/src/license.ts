// License signing/verification with Web Crypto (ECDSA P-256 / SHA-256, raw r||s = IEEE-P1363).
// This produces tokens byte-compatible with the C# app's LicenseToken (which now verifies P1363).

export interface LicensePayload {
  licenseId: string;
  email: string;
  product: string;
  tier: string;
  features: string[];
  issuedUtc: string;
  expiresUtc?: string;
}

const enc = new TextEncoder();
const dec = new TextDecoder();

function b64urlEncode(bytes: Uint8Array): string {
  let bin = "";
  for (const b of bytes) bin += String.fromCharCode(b);
  return btoa(bin).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
}

function b64urlDecode(s: string): Uint8Array {
  s = s.replace(/-/g, "+").replace(/_/g, "/");
  while (s.length % 4) s += "=";
  const bin = atob(s);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

function derFromBase64(b64: string): Uint8Array {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

export async function importPrivateKey(pkcs8Base64: string): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    "pkcs8",
    derFromBase64(pkcs8Base64),
    { name: "ECDSA", namedCurve: "P-256" },
    false,
    ["sign"],
  );
}

export async function importPublicKey(spkiBase64: string): Promise<CryptoKey> {
  return crypto.subtle.importKey(
    "spki",
    derFromBase64(spkiBase64),
    { name: "ECDSA", namedCurve: "P-256" },
    false,
    ["verify"],
  );
}

export async function signLicense(payload: LicensePayload, privateKey: CryptoKey): Promise<string> {
  // Compact JSON, omit expiresUtc when absent (matches the C# WhenWritingNull policy).
  const json = JSON.stringify(payload);
  const payloadBytes = enc.encode(json);
  const sig = new Uint8Array(
    await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, privateKey, payloadBytes),
  );
  return b64urlEncode(payloadBytes) + "." + b64urlEncode(sig);
}

export async function verifyToken(
  token: string,
  publicKey: CryptoKey,
): Promise<{ ok: boolean; payload?: LicensePayload }> {
  const parts = token.trim().split(".");
  if (parts.length !== 2) return { ok: false };
  try {
    const payloadBytes = b64urlDecode(parts[0]);
    const sig = b64urlDecode(parts[1]);
    const ok = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      publicKey,
      sig,
      payloadBytes,
    );
    if (!ok) return { ok: false };
    return { ok: true, payload: JSON.parse(dec.decode(payloadBytes)) as LicensePayload };
  } catch {
    return { ok: false };
  }
}

export function newLicenseId(): string {
  const b = crypto.getRandomValues(new Uint8Array(6));
  return [...b].map((x) => x.toString(16).padStart(2, "0")).join("");
}
