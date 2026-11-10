# TaggJack (TaggJack) — Evolabs portal licensing

Product code: **`TAGGJACK`**  
Portal: **https://admin.evolabs.co**

Same model as CrewCycle: hardware-bound activation request → Evolabs `LICENSE_ADMIN` approves with expiry → signed lease JWT → local verify with embedded public key.

## Customer flow

1. Install / run TaggJack.
2. Activation window shows the **hardware key** (SHA-256 of machine fingerprint).
3. Optionally enter company name / contact.
4. Click **Request activation**.
5. Evolabs admin opens Licenses → product **TAGGJACK** → pending request → **Approve** (set `expiresAt`).
6. Customer clicks **Check license** → app starts.
7. Lease is cached under `%APPDATA%\TaggJack\taggjack-license-lease.jwt`.

Offline grace: **72 hours** after last successful validation if the portal is unreachable. Refresh interval: **12 hours**.

## Admin flow

1. Sign in to https://admin.evolabs.co as a `LICENSE_ADMIN` user.
2. Open **Licenses**.
3. Select product **TAGGJACK**.
4. Approve the pending request (or Create license shell, then authorize / approve).

## Enforcement

- Startup blocks until licensed (no “limited mode” bypass).
- Print / scheduled print call `RequireLicensed()`.
- Banner on main window for `EXPIRING` / `GRACE`.

## Constants (hardcoded — not in config.json)

| Setting | Value |
|---------|--------|
| Portal | `https://admin.evolabs.co` |
| Product | `TAGGJACK` |
| Public key | embedded `Resources/keys/license-public.pem` |

## Notes

- Offline TJ11 / AES `license.dat` activation is removed.
- Vendor `--license-generator` CLI entry is removed from startup.
- Do not rotate the portal RSA keypair without updating the embedded public PEM in both CrewCycle and TaggJack.
