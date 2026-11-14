# Waveshare Modbus POE ETH Relay 16CH: findings for 219volts

Read from the official wiki (https://www.waveshare.com/wiki/Modbus_POE_ETH_Relay_16CH), 14 Nov 2026. Board not yet in hand; items marked **TEST** must be checked on the real board.
Chosen as the development board and the trial unit for the first client (16 spots; a second board can be cascaded via the second Ethernet port).

## Hardware
- 16 relays, each **1NO + 1NC**, ≤10 A at 250 VAC / ≤10 A at 30 VDC. Bigger loads or large switch-on surges → relay drives a contactor.
- Power: PoE (IEEE 802.3af), DC 5.5×2.1 barrel jack, or terminal block, **7–36 V**. Max ~7 W. Power supply not included (12 V 1 A wall adapter is enough at a desk).
- Two identical Ethernet ports (communication or cascade). **No RS485 port**: "Modbus RTU" on this board only means RTU frames sent over the network.
- Operating temperature −15 °C to 70 °C.

## Failure behaviour (most important)
- **Wire every spot to COM + NO.** Unpowered or dead board → coil off → NO open → spot **off** (fail-safe, no free play).
- **Power-on state is not documented.** No "remember last state" setting is mentioned. **TEST 1:** turn some relays on, pull power, plug back in: do they come back off or as they were?
- **No watchdog / communication-loss behaviour documented.** Expect relays to keep their last state if the PC or cable dies. **TEST 2:** turn a relay on, unplug the network cable: does it stay on?
- Software rule regardless of results: **when a board comes back online, re-send the expected state of all its channels** (FC 0F write-all). This is the automatic form of the planned drift detection.

## Built-in timer (safety net)
- "Flash on" (register 0x0200 + relay) and "flash off" (0x0400 + relay), interval = value × 100 ms, **max 0x7FFF ≈ 54.6 min** (own calculation from the wiki's formula).
- Use flash-on as the equivalent of Shelly's `toggle_after`: relay goes on and turns itself off after the interval, even if the PC dies. Sessions longer than ~54 min: service re-arms the timer periodically (e.g. every 30 min).
- **TEST 3:** start a flash-on timer, then send a plain "on" to the same relay: is the timer cancelled? (Same open question as Shelly.) Also check what a new flash-on does to a running one.

## Modbus details for the driver
- Function codes: 01 read coils (relay states), 03 read holding register, 05 write single coil, 0F write multiple coils. (06 listed, no example.)
- Coils 0x0000–0x000F = relays 1–16. Values: **0xFF00 on, 0x0000 off**, 0x5500 toggle.
- 0x00FF = all relays (FC 05). 0x0100–0x010F / 0x01FF = toggle registers.
- **Never use toggle in our software.** A retried toggle flips the relay back. Only send explicit on/off, so every command can be safely repeated.
- Read status: FC 01, start 0x0000, quantity 16 → 2-byte bitmap, bit 0 = relay 1, low byte first. Use this for the reachability check and to read actual state.
- Write all: FC 0F, start 0x0000, quantity 16, 2-byte bitmap (low byte first).
- Software version: FC 03, register 0x8000 (0x012C = 300 → V3.00). Good for device info / health page. Device address register 0x4000.
- Unit ID (slave address) fixed at **1**; devices are identified by IP.
- Exceptions: function code + 0x80; codes 01 illegal function, 02 illegal address, 03 illegal value, 04 server failure, 05 acknowledge, 06 busy.
- Modbus TCP frame = RTU frame without CRC + 6-byte header (transaction id, protocol id 0, length). Standard libraries (FluentModbus / NModbus) handle this.

## Setup at install (installer checklist)
- Configured with Waveshare's **Vircom** Windows tool (Device → Auto Search) or the board's web page.
- **Default transfer protocol is "None" = RTU frames over TCP.** For our driver set **Transfer Protocol = Modbus TCP** → port becomes **502**.
- **Set Modbus Gateway Type to non-storage (Multi-host).** The default "storage" type re-sends queries on its own and can make the board stop answering.
- Set a **static IP** (or DHCP reservation, decision 19). Out-of-box IP not stated; the wiki's web example uses 192.168.1.200. Use Vircom Auto Search to find it.
- **Web page password is empty or 123456 by default: change it.**
- Factory reset: open the case, short the pad marked DEF for >5 s → IP 192.168.1.254. Web password reset uses the same pad.
- Don't download an HTTP config file from the web page: it wipes the web page resources (they must then be re-uploaded).

## Not documented (decide or test)
- Number of simultaneous TCP connections. **TEST 4:** keep the service connected and open the web page / Vircom at the same time. Plan: the service is the only Modbus client.
- Firmware update procedure (only a BootLoader link).
- DHCP vs static default.
- Real switching speed / response time. **TEST 5:** time 100 on/off commands and status reads.
