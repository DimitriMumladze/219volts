# 219volts: architecture decisions (running log)

Started 8 Nov 2026. Each entry: what was decided and why. Open questions at the bottom.

**Source of truth:** this file in the repo, `docs/architecture-decisions.md`. Claude Code loads it through `CLAUDE.md`. When a decision changes in a claude.ai chat, Claude writes the change to the repo file (through the desktop app) and to the project copies (`architecture-decisions.md`, `claude/architecture-decisions.md`), keeping all three identical. If the repo can't be reached from a chat, the change is made in the project copies and marked "not yet in repo" at the top.

Infrastructure overview doc (for sharing): https://claude.ai/artifact/GpaxFJ9xCyRLDXbXmtZuBk
Brief for the frontend + payments developer (friend): https://claude.ai/artifact/Bb6ka2LfDuiFMbLbp9yiSx
Detailed Auth design: `docs/superpowers/specs/2026-11-11-venue-auth-design.md` (project: `claude/venue-auth-design-progress.md`).
Devices v1 input: `docs/devices-v1-decisions.md`. Relay board details: `docs/hardware/waveshare-findings.md`.

## Decided
1. **Packages scale by channel count.** Venues range from ~5 to 32+ spots. Relay hardware is stacked in standard building blocks (e.g. 16-channel Ethernet relay boards in the panel; Shelly Pro 4PM for very small sites). See 50.
2. **No all-in-one unit.** The controller is either the client's own PC or a mini PC we supply. Relay boards in the electrical panel are separate and reached over the LAN.
3. **Relays are never exposed to the internet.** Only the venue's local software talks to them, over the LAN. No vendor clouds (Shelly Cloud etc.) in the main path.
4. **Venue software connects out to the cloud.** The local service opens and keeps a persistent connection (SignalR) to our cloud; the cloud pushes commands down it. No port forwarding or static IP at the venue.
5. **The local software is the base product; QR payments are an add-on package.** The venue works fully on its own; the cloud layer is built on top.
6. **QR payment requires the venue to be online.** When the QR page opens, the cloud checks the spot's live status via the venue connection. If the venue is offline, payment is not offered.
7. **Local service + web UI (concept agreed 8 Nov).** ASP.NET Core app running as a Windows Service holds timers, DB, relay drivers and the cloud link, and also serves the UI as static files. Offline work comes from the service, not from the UI. Tablets/phones on the LAN open the same address.
   - UI technology: **React + TypeScript + Vite suggested; the frontend developer decides** (see 34). Node only at build time; TypeScript client generated from the API's OpenAPI description; `@microsoft/signalr` for live updates.
8. **Same UI and API contract locally and remotely (agreed).** Local service exposes `/api/...`; cloud exposes `/venues/{id}/api/...` and forwards requests down the venue's open connection. Differences: remote login is personal email + password (see 44) instead of name + PIN, "venue offline" banner remotely. No local-only actions (35).
9. **Self-update through the cloud (agreed).** Release = self-contained `dotnet publish` zip (includes .NET runtime and the UI files), built by GitHub Actions. Signed manifest + zip in cloud storage; cloud tells venues a version is available; local service downloads and verifies; a separate small Updater service stops/swaps/starts/health-checks and rolls back on failure. Side-by-side version folders. Staged rollout. Flash drive = same zip, run Updater manually.
   - Restart-safe design: sessions stored as end timestamps in the DB; relays keep state during restart.
   - Open UIs detect a version change over SignalR and reload; API changes stay backward compatible for one version.
10. **Device control lives only in the venue's local service.** The cloud has no Devices module and never knows relay IPs or channel numbers. Cloud ↔ venue messages are business-level ("start/extend session on spot 12 for 60 min, payment X"); the local service maps spots to relay channels.
   - Local service modules: Devices (drivers + channel mapping), Tables/Sessions (timers, pricing), Local auth, Cloud link (SignalR client), Health.
   - Cloud modules: Venue gateway (SignalR hub, request forwarding, online status), Payments (QR payments + venue subscription), Accounts (remote user accounts, `VenueUsers`, invitations), Releases. Later: booking.

### Decided 10 Nov 2026
11. **One relay channel per spot.** A spot is anything that is switched (monitor, tennis table, billiard table); everything on it is on one circuit.
12. **Local DB: SQLite + EF Core.** Only the local service touches it.
13. **Cloud access to venue data: live via SignalR now, sync-ready.** Nothing of the venue's data is stored in the cloud yet (exception: remote accounts, 44). An outbox table exists in SQLite from day one, so a one-way venue → cloud sync can be switched on later.
14. **Cloud DB: PostgreSQL.** Hosting provider: chosen by the friend (42).
15. **Payment provider: Flitt.** Hold → confirm → capture: create order with `preauth = Y`, send command to venue, capture only after the venue confirms the spot is on; release the hold if it doesn't.
16. **QR = fixed sticker per spot.** Customer can start or extend; preset packages plus custom duration.
17. **Prices set by the owner, editable locally or remotely** (same data; lives in the venue SQLite, remote edits go down the SignalR connection).
18. **Installation: our team installs on site with a setup program.** Normal desktop shortcut on the counter PC (no kiosk auto-start).
19. **Relay addressing: static IP or DHCP reservation**, set during install; IP stored per device.
20. **Venue software = one ASP.NET Core app, a modular monolith** (confirms and refines 7). Runs as a Windows Service. Devices is an isolated module inside it: own registration, own DB tables, other modules reach it only through a small interface, so it can be split into its own Worker Service later if a real reason appears.
21. **No RabbitMQ on the venue side.** Venue modules call each other in-process. RabbitMQ is used only where separate programs naturally need to talk (the cloud services, see 40).
22. **Windows only for shipping.** Controller = Windows PC (client's own, or a cheap N95/N100 mini PC with Windows 11 Pro, ~€200–300). Linux was considered and set aside: Raspberry Pi prices rose sharply in 2026 (RAM shortage), so hardware savings are small, while installer/updater/testing/support would double.
   - **Code still follows cheap cross-platform rules** so Linux stays possible: `Path.Combine` for paths, data folder in config, no registry / Windows-only APIs, mind case-sensitive file names.
23. **Venue app structure: one project, Clean Architecture layer folders at the top, features inside each layer** ("layout Y", corrected 11 Nov). E.g. `Domain/Devices/`, `Application/Devices/`, `Infrastructure/Devices/`, `Endpoints/`. Keeps repo standard §3.3 (one project); layers are the main structure (easy to learn, and each top folder can later become its own project = full Clean Architecture). Accepted trade-offs: a feature's code is spread over several folders, module walls are a convention, and splitting Devices out later means collecting its pieces from each layer. Considered: features-first with layers inside ("layout X"), one project per module, one project per layer.
   - Follow-up: repo standard §3.4 (folders by feature) and §3.5 (folders inside a feature) need rewriting for this layout.

### Decided 11 Nov 2026
24. **Fresh start on `feature/devices-manager-project`.** New project from the template; copy over only `CODING_STANDARDS.md`, `CLAUDE.md`, `.gitignore`, `Common/Result.cs` and chosen setup parts from `feature/demo-api`. Demo code stays on its branch for reference.
25. **Venue app project name: `TwoNineteenVolts.Venue`.** "Device manager" is the Devices feature inside it, not the app name (the app will also hold Sessions, Auth, CloudLink, Health).
26. **Build order: Local auth first, then Devices v1.** Reason: an audit trail of who did what is required, so identity must exist before features that need auditing. Each gets its own spec.
27. **Devices v1 scope and rules (agreed; spec written after Auth):**
   - Register a device (IP, kind, channel count) in SQLite; create spots and map each to device + channel; turn a spot on/off and read its state via the device's local protocol (Shelly RPC or Modbus TCP, see 50).
   - Continuous background reachability check per device: every **10 s** (config), **2 s** timeout per check (constant), **offline after 3 missed checks in a row**, back online on first reply. Status (online/offline, last seen) kept **in memory only**.
   - Switching a spot on an offline device: **try anyway, report the real result**; a successful reply flips the device back online. A failure is never shown as success.
   - `IRelayDriver` interface **added now** (Application layer), Shelly as first implementation; justified under §1.1 by the decided Ethernet boards.
   - `/health` endpoint for the updater (decision 9) proposed; to confirm.
   - Registering devices / mapping spots requires the Manager role.
   - Later, with Sessions: drift detection ("should be on but is off"), on-device auto-off timer, re-sending expected states when a board comes back online.
28. **Local auth (design sections 1–4 agreed; full detail in the auth spec):**
   - One login per employee; users in the venue SQLite (works offline).
   - Login = **tap your name, then 4-digit PIN** (same length for all). Login screen lists active users (not Support).
   - **No lockout; slow down instead:** after 3 wrong PINs for a user, each next attempt must wait 1 → 2 → 4 → 8 → 16 → 30 s (max); correct PIN resets.
   - **Roles: Staff, Manager, Support.** Manager = setup (devices, spots) + user management. Support = everything a Manager can, plus future support-only functions managers must not access. Policies: `Staff` (all three), `Manager` (Manager + Support), `Support` (Support only).
   - **Only managers set PINs** (for anyone, including their own). PINs stored hashed (`PasswordHasher`).
   - **Deactivate, never delete**; users can be **reactivated** and **renamed**. **Names unique forever** (checked against current names of all users), case-insensitive. **At least one active Manager always**; Support doesn't count.
   - **Auto-logout after 1 hour of inactivity** (config), same for all devices; current user always visible + one-tap switch-user.
   - **Cookie authentication + in-memory session store** (`ITicketStore`): no per-request DB lookup; deactivation/role change end sessions immediately; background check records `auth.session_expired`; **app restart logs everyone out** (accepted). Not ASP.NET Identity, not JWT, not `AddSession()`.
   - Authorization rules are written against **claims** (`ClaimsPrincipal`), so a second scheme for remote requests can plug in later without changing endpoints.
   - **First Manager + Support account created by a setup command** run by our team on the venue PC (`TwoNineteenVolts.Venue.exe setup`). No first-run web screen.
   - **"219volts Support" account:** one per venue, long random password (kept in our password manager), logs in via hidden `/support` route; **hidden from the user list, cannot be changed or deactivated by managers**; **shown truthfully in the audit log**; **disclosed in the client agreement**.
   - **HTTP in v1; HTTPS on the venue LAN is its own task before the first real install.**
29. **Audit log:** records every change (switch on/off, device/spot/user changes), logins/logouts (incl. auto-logout, switch-user) and failed logins; each entry = when, who, action, target, **source (local/remote)**. Viewing/reading is not logged. **Managers only** can view it. **Kept 1 year**, older entries deleted automatically. Audit is its own feature with a small `IAuditLog` interface used by other features.
30. ~~**Remote access via personal cloud accounts linked by a one-time code, with 2FA.**~~ **Replaced on 13 Nov 2026 by 44.** Still valid: the `RemoteAccessAllowed` switch on `User` (added now, no role restriction; managers decide whom to enable); remote requests act as the linked local user with that user's role; deactivating the local user blocks remote access immediately.
31. **Runs as a Windows Service from day one** (`AddWindowsService()`; F5 still runs it as a console app).
32. **Tests start now:** `tests/TwoNineteenVolts.Venue.Tests` (xUnit). Auth is the first feature with real rules; repo standard §8 to be rewritten in the spec.
33. **One `VenueDbContext`** for the whole venue app, in `Infrastructure/Data/`; each feature's EF configuration lives in `Infrastructure/<Feature>/`. One migration history; migrations applied automatically at startup. Data folder in config (default `C:\ProgramData\219volts\`, file `venue.db`).

### Decided 12–13 Nov 2026 (frontend/payments brief)
34. **Work split:** Lasha's friend builds the venue staff UI, the owner remote portal, the customer QR page and the cloud Payments module (.NET, Flitt), including venue subscription billing. Lasha builds the venue local service and the cloud gateway. UI framework: React + TS + Vite is a suggestion; the friend decides.
35. **All actions are available remotely** (owner portal), including device/spot setup. No local-only actions.
36. **QR link holds only constant values:** venue code + spot code (e.g. `/q/{venueCode}/{spotCode}`). Merchant ID, prices and staff are kept and managed in the cloud. The term "spot" may be renamed.
37. **QR page:** shows remaining time; customer adds time by typing minutes, with suggestions 5, 10, 15, 30, 60. **Price = minutes / 60 × owner's hourly rate** (pro-rated), calculated on the server. Languages: **Georgian (primary), English, Russian.** On-screen receipt only (amount, minutes, order number); no fiscal receipt part.
38. **Two SignalR links:** cloud ↔ venue, and cloud ↔ QR page. The QR page counts down in the browser; the cloud pushes corrections and payment results.
39. **Wait for the venue's answer after a payment hold: 1 minute**, then release the hold.
40. **RabbitMQ is advised in the cloud wherever there is an occasion** (separate services passing messages). Still none on the venue side (21).
41. **Venue subscription (monthly fee) in the cloud:** card auto-charge through Flitt recurring payments. **7-day grace period**, then the venue is disabled: **everything stops** (staff UI shows only a "subscription expired" screen). Enforcement works offline: the cloud sends a **signed "paid until" date** over SignalR; the venue stores it and blocks itself after the date (+ grace) passes; a new date unlocks it. Packages/prices to be decided.
42. **Cloud hosting provider: chosen by the friend.**
43. **One Git repository** for venue app, cloud and UI.

### Decided 13 Nov 2026 (control panel deep dive)
44. **Remote login = personal email + strong password per user.** (Same night: a shared venue username/password was considered and dropped.) Only users with "allow remote access" get one. **Stored in the cloud (PostgreSQL)**; the cloud checks the password, then acts as the linked local user with that venue's role. **No 2FA for now** (can be added later).
   - **One account, many venues:** one cloud user per email; many-to-many `VenueUsers` table; at each venue the account is linked to that venue's local user (role comes from there). After login, a list of only that person's venues; with one venue, straight in.
   - **Email invitations (agreed 13 Nov):** a manager turns on remote access and enters **only the email**; managers never type or see passwords. No account yet → email link to set own password. Account exists → invitation the person must **accept** before the venue appears. Needs the venue online. Users page shows invitation status (sent, accepted).
   - **Changing the password:** only the user (change-password screen) or via a "forgot password" email link.
   - Deactivating the local user or turning remote access off blocks remote login to that venue immediately.
45. **Control panel layout:** collapsible side menu like modern code editors (icons only when collapsed, icons + titles when expanded); current user, switch-user and log-out in the menu; pages shown by role. Pages: Dashboard (everyone), Devices and spots setup, Prices, Users, Audit log, Statistics (all Manager).
46. **Dashboard = one card per spot.** Card shows name, on/off, live countdown + end time, device-offline warning. A three-dot button opens a modal with start/extend/stop (duration input + quick values 5, 10, 15, 30, 60, same as the QR page), session details (start, end, who started it — staff or QR —, amount) and device info (relay + channel, online/offline, last seen).
47. **Audit page filters:** date range, user, action type, source, spot. Loaded page by page from the server.
48. **Statistics page (ideas, not decided), Manager only:** revenue per day/week/month, spot usage, busy hours, per staff. Needs session history on the backend.
49. **Counter payments are recorded as an amount only** (no cash/terminal split), so revenue covers both counter and QR payments.

### Decided 14 Nov 2026 (relay hardware and connections)
50. **Supported relay hardware is a short list, chosen per venue at install time** (refines 1). Every venue's setup is individual; the software stays the same.
   - **Shelly Pro 4PM** (4 channels) for small venues; development on the existing Shelly 1PM Mini Gen4 (same local Switch RPC).
   - **Waveshare Modbus POE ETH Relay** boards (8 / 16 / 30 channels, Modbus TCP over Ethernet) for larger venues.
   - **Waveshare Modbus RTU (RS485)** boards are a supported future option for long cable runs (not in Devices v1).
   - **Two drivers behind `IRelayDriver`:** Shelly (local HTTP RPC) and Modbus. The Modbus driver later covers RS485 too; only the transport differs.
   - Considered: Shelly Pro 4PM only (too expensive and bulky at 32 spots), Waveshare only, RS485 everywhere (needs its own cable from PC to panel and a USB adapter tied to the PC).
51. **The link from the controller PC to the relays is always wired** (Ethernet, or RS485 later), **never the venue Wi-Fi.** Screens (laptops, tablets, phones) can be on any network; they are only browsers. When the counter is far from the panel, the controller goes near the panel. Wi-Fi bridges only on our own separate access point, as a last resort.
52. **Wiring rule: every spot on COM + NO.** A dead or unpowered relay board means all spots off. A "fail-on" design (NC wiring + heartbeat timer) was considered and **rejected** to keep the software simple. Manual Auto/Off/Hand bypass switches in the panel are possible per venue (no software impact); not decided as standard.
53. **Development / trial hardware: one Waveshare Modbus POE ETH Relay 16CH** (being bought), with a 12 V wall adapter, a USB-to-Ethernet adapter and a cable for the laptop. It is the trial unit for the first client (one month free). Driver facts and the five tests to run on arrival are in `docs/hardware/waveshare-findings.md`.
   - Driver rules from the findings: Modbus TCP port 502, unit ID 1, coils 0–15 = channels 1–16, only explicit on/off (**never toggle**, so retries are safe).

## Open
- HTTPS on the venue LAN (before first real install).
- Subscription packages and prices.
- QR: "Start" on a spot that just became busy (refuse or extend?); custom minutes min/max; rounding of the pro-rated price.
- Blocked venue: what happens to sessions running at that moment; protection against changing the PC clock; warnings during the grace period.
- Controller mini PC model. Proposal: dual Ethernet port (one to relays, one to router), Windows 11 Pro preinstalled, BIOS "power on after power loss"; candidate Beelink EQ14 (N150), cheaper alternative GMKtec G2 Plus.
- Manual bypass switches: standard in every package, or optional extra?
- Waveshare board tests on arrival (power-loss state, cable unplugged, timer vs plain "on", simultaneous connections, response time).
