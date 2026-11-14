# Devices v1: decisions and notes on Spots

Input for planning the Devices v1 spec. Compiled 14 Nov 2026 from the 219volts decision log (`architecture-decisions.md`, decisions 1–49) plus the hardware decisions made on 14 Nov 2026.
Status markers: **Decided** = agreed with Lasha. **Proposal** = recommended, not yet confirmed; the planner may adopt or raise it.

---

## 1. Decision 27: Devices v1 scope and rules (Decided, 11 Nov 2026)

- Register a device (IP, kind, channel count) in SQLite; create spots and map each to device + channel; turn a spot on/off and read its state via the device's local protocol.
- Continuous background reachability check per device: every **10 s** (config), **2 s** timeout per check (constant), **offline after 3 missed checks in a row**, back online on first reply. Status (online/offline, last seen) kept **in memory only**.
- Switching a spot on an offline device: **try anyway, report the real result**; a successful reply flips the device back online. A failure is never shown as success.
- `IRelayDriver` interface **added now** (Application layer), Shelly as first implementation; justified by the decided Ethernet relay boards (see section 3).
- `/health` endpoint for the updater (decision 9) proposed; to confirm.
- Registering devices / mapping spots requires the **Manager** role (policy `Manager` = Manager + Support).
- Later, with Sessions (not v1): drift detection ("should be on but is off"), on-device auto-off timer.

Related rules that bind Devices v1:
- **10. Device control lives only in the venue's local service.** The cloud never knows relay IPs or channel numbers; cloud ↔ venue messages are business-level ("start session on spot 12"). The local service maps spots to relay channels.
- **19. Relay addressing: static IP or DHCP reservation**, set during install; IP stored per device.
- **20/23. Devices is an isolated feature** inside `TwoNineteenVolts.Venue` (one project, layer folders at the top: `Domain/Devices/`, `Application/Devices/`, `Infrastructure/Devices/`, `Endpoints/`). Other features reach it only through a small interface.
- **29. Audit log:** every change is audited (switch on/off, device/spot changes) via `IAuditLog`: when, who, action, target, source (local/remote).
- **33. One `VenueDbContext`**; Devices' EF configuration lives in `Infrastructure/Devices/`.
- **35. Every action is also available remotely** (owner portal), including device/spot setup. No local-only endpoints.

## 2. Notes on Spots

- **11. One relay channel per spot (Decided).** A spot is anything that is switched: a monitor, a tennis table, a billiard table. Everything on a spot is on one circuit.
- A spot maps to exactly one (device, channel). A channel belongs to at most one spot. (Proposal: enforce both with unique constraints.)
- Spots have a **name** (shown on the dashboard) and a **spot code** used in the QR link `/q/{venueCode}/{spotCode}` (36). The code must stay constant once QR stickers are printed (Proposal: code immutable after creation; name editable).
- The term **"spot" may be renamed** later (36): keep the word out of anything hard to change (public URLs excepted, which use the code).
- **Dashboard (46)** shows one card per spot: name, on/off, live countdown + end time (Sessions, later), device-offline warning, and device info (device + channel, online/offline, last seen). Devices v1 must expose enough for this: spot → device → online status / last seen.
- Spot on/off state shown in the UI is the **state read from the relay**, not an assumed state.
- Sessions, timers and prices are **not** part of Devices v1; they use Devices through its interface.

## 3. Hardware and connection decisions (14 Nov 2026)

### Decided
- **Relay hardware is a short supported list**, chosen per venue at install time:
  - **Shelly Pro 4PM** (4 channels) for small venues. Development uses the existing **Shelly 1PM Mini Gen4** (1 channel); same local Switch RPC API.
  - **Waveshare Modbus POE ETH Relay 16CH** (Modbus TCP over Ethernet) for larger venues; 8CH and 30CH variants exist with the same protocol. **One 16CH board has been ordered** as the development board and the trial unit for the first client.
  - Waveshare **Modbus RTU (RS485)** boards are a supported future option (long cable runs); not in v1.
- **Two drivers behind `IRelayDriver`:** Shelly (local HTTP RPC) and Modbus. The Modbus driver will later also cover RS485 boards; only the transport differs.
- **Relay links are always wired** (Ethernet, or RS485 later). Never the venue Wi-Fi. Screens (tablets/laptops) can be on Wi-Fi; they are only browsers.
- **Wiring rule: every spot on COM + NO.** A dead or unpowered board means all spots off.
- **No "fail-on" design** (NC wiring + heartbeat) — rejected to keep the software simple.
- No vendor clouds in the path (decision 3); Shelly is reached by local RPC only.

### Proposal (recommended, not yet confirmed)
- Controller PC: a mini PC with **two Ethernet ports** (one to the relay boards, one to the router), Windows 11 Pro, BIOS set to power on after power loss. Candidate: Beelink EQ14 (N150). Not relevant to the code.
- Optional per-venue **manual Auto/Off/Hand switches** in the panel as a hardware bypass. No software impact in v1.

## 4. Driver facts the plan needs

### Shelly (Gen2+ local RPC) — see also the demo code on branch `feature/demo-api`
- Switch: `GET http://<ip>/rpc/Switch.Set?id=<channel>&on=true|false`; status: `Switch.GetStatus?id=<channel>`.
- `toggle_after=<seconds>` gives an on-device timer (for Sessions later).

### Waveshare Modbus POE ETH Relay 16CH — details in `docs/hardware/waveshare-findings.md`
- Modbus TCP, port **502**, unit ID **1** (devices identified by IP). Board must be set to Transfer Protocol = Modbus TCP and Gateway Type = non-storage at install (Vircom tool).
- Coils **0x0000–0x000F** = channels 1–16. FC 05 write single coil: **0xFF00 = on, 0x0000 = off**. FC 01 read coils (start 0, qty 16) returns all states as a bitmap (bit 0 = channel 1). FC 0F writes all channels at once.
- **Never use toggle (0x5500 / toggle registers).** Only explicit on/off, so every command is safe to retry.
- Software version: FC 03 register 0x8000 (useful for device info).
- Board is not in hand yet: the driver can be built and tested against a Modbus TCP server in tests (e.g. FluentModbus includes a server) or a simulator. (Proposal.)
- No documented watchdog; power-on state unknown (to be tested). Hence the Proposal below.

### Proposal for v1 behaviour
- **Reachability check** per device kind: Shelly `Switch.GetStatus` / `Shelly.GetStatus`; Modbus FC 01 read coils. A successful read also refreshes the known channel states.
- **Re-sync on reconnect is NOT in v1** (needs Sessions to know the expected state); note it as a Sessions-era item together with drift detection.
- Device kinds as an enum, e.g. `ShellyGen2` (channel count from config: 1 or 4) and `WaveshareModbusTcp` (8/16/30), each mapped to its driver.

## 5. Open (not decided)
- `/health` endpoint shape (decision 27, "to confirm").
- HTTPS on the venue LAN (separate task before the first real install).
- Waveshare board tests once it arrives: state after power loss, behaviour when the cable is unplugged, whether a plain "on" cancels a running flash timer, simultaneous TCP connections, response time.
