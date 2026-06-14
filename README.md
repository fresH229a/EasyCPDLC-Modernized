# EasyCPDLC – Modernized

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![Network](https://img.shields.io/badge/network-VATSIM%20%2B%20Hoppie-blue)
![Status](https://img.shields.io/badge/status-community%20fork-orange)

A modernized and visually refreshed fork of **EasyCPDLC**, the lightweight CPDLC client for pilots flying on **VATSIM** via the **Hoppie ACARS** network.

This build updates the project for **.NET 10** and adds a redesigned cockpit-style DCDU interface, Airbus/Boeing visual styles, smarter weather and clearance handling, MSFS/Flow Pro integration, and quality-of-life features for day-to-day online flying.

---

## Index

- [Screenshots](#screenshots)
- [What is EasyCPDLC?](#what-is-easycpdlc)
- [What changed in this fork?](#what-changed-in-this-fork)
- [Features](#features)
- [MSFS / Flow Pro integration](#msfs--flow-pro-integration)
- [Flow Pro setup](#flow-pro-setup)
- [Flight plan reload workflow](#flight-plan-reload-workflow)
- [Smart message handling](#smart-message-handling)
- [Status indicators](#status-indicators)
- [ATIS, METAR and automatic arrival logic](#atis-metar-and-automatic-arrival-logic)
- [Free text cooldown](#free-text-cooldown)
- [Message filtering](#message-filtering)
- [Requirements](#requirements)
- [Installation](#installation)
- [First start](#first-start)
- [Project status](#project-status)
- [Credits](#credits)
- [Disclaimer](#disclaimer)
- [License](#license)

---

## Screenshots

| Login | Airbus-style | Boeing-style |
|---|---|---|
| ![EasyCPDLC login screen](assets/screenshots/login.png) | ![EasyCPDLC DCDU Airbus-style panel](assets/screenshots/dcdu-airbus1.png) | ![EasyCPDLC DCDU Boeing-style panel](assets/screenshots/dcdu-boeing1.png) |

---

## What is EasyCPDLC?

**EasyCPDLC** is a standalone CPDLC client for flight simulation. It allows pilots to use CPDLC-style communication with compatible ATC stations on VATSIM without needing an aircraft that has native CPDLC support.

The client connects using your:

- **Hoppie logon code**
- **VATSIM CID**

After connecting, EasyCPDLC can be used for common datalink workflows such as ATC logon, clearance requests, TELEX-style messages, METAR/ATIS requests, and CPDLC communication during online flights.

---

## What changed in this fork?

This fork focuses on modernization, compatibility, cockpit-style visuals, and smoother simulator workflow.

### Highlights

- Updated for **.NET 10**
- Refreshed login screen with a cleaner VATSIM/Hoppie connection flow
- Redesigned DCDU-style interface
- Airbus-inspired and Boeing-inspired panel variants
- Improved button styling, spacing, shadows, and cockpit-like visual hierarchy
- Redesigned CPDLC, ATC, TELEX, METAR, and ATIS screens
- Smart message overview for weather and clearance messages
- Status indicators for clearance, PDC availability, and ATIS availability
- Flow Pro integration for opening EasyCPDLC directly from inside MSFS
- Flight plan reload workflow for multi-leg flying or callsign changes
- More modern project/runtime foundation for future updates

---

## Features

- Connect to the Hoppie ACARS network
- Use your VATSIM CID for flight/session lookup
- Log on to CPDLC-equipped ATC stations
- Send and receive CPDLC messages
- TELEX-style messaging support
- Datalink clearance workflow support
- METAR and ATIS request support
- Smart ATIS/METAR message summaries
- Lightweight standalone desktop client
- Cockpit-style DCDU interface skins
- Airbus-style and Boeing-style display options
- Message filtering by type
- Unread message highlighting and reminder sound
- Weather message cache
- Exportable message log
- Optional MSFS Flow Pro shortcut support
- System tray support with Show / Hide / Exit
- Flight plan reload button for new VATSIM flight plans or callsign changes

---

## MSFS / Flow Pro integration

EasyCPDLC can be opened from inside Microsoft Flight Simulator using **Parallel 42 Flow Pro**.

The application registers a Windows URI protocol:

```text
easycpdlc://show
```

When this URI is called, EasyCPDLC brings its already running window back to the front. This allows you to open the CPDLC client from inside MSFS without going back to the desktop.

There is also an optional toggle URI:

```text
easycpdlc://toggle
```

For normal use, `easycpdlc://show` is recommended.

### Notes

- This works best with MSFS in borderless/windowed fullscreen.
- In exclusive fullscreen, Windows may still show the taskbar or change focus behavior.
- EasyCPDLC uses a tray icon so it can run in the background without a normal taskbar button.
- If the URI does not work, start EasyCPDLC once normally first. The URI protocol is registered on application start.

---

## Flow Pro setup

To create a Flow Pro button that opens EasyCPDLC in-game:

1. Start EasyCPDLC once normally.
2. Confirm that this works in Windows:
   - Press `Win + R`
   - Enter:

     ```text
     easycpdlc://show
     ```

   - EasyCPDLC should appear.
3. Open MSFS.
4. Open the Flow Pro wheel editor.
5. Add a **Custom Script Widget**.
6. Open the widget editor.
7. Paste this JavaScript code:

```js
run(() => {
    this.$api.command.open_browser("easycpdlc://show");
    return 250;
});

state(() => {
    return "ECPDLC";
});

info(() => {
    return "Open EasyCPDLC";
});

style(() => {
    return "active";
});
```

8. Save/compile the widget.
9. Click the widget in MSFS to bring EasyCPDLC forward.

### Optional toggle version

Use this instead if you want a toggle-style button:

```js
run(() => {
    this.$api.command.open_browser("easycpdlc://toggle");
    return 250;
});

state(() => {
    return "ECPDLC";
});

info(() => {
    return "Toggle EasyCPDLC";
});

style(() => {
    return "armed";
});
```

---

## Flight plan reload workflow

EasyCPDLC includes a **RLD FP** button for reloading your current online flight data.

Use this when:

- you have landed and continue with another leg
- you filed a new VATSIM flight plan
- your callsign changed
- your departure/arrival airport changed
- EasyCPDLC still shows data from the previous flight

The **RLD FP** action reloads:

- VATSIM pilot data
- current callsign
- filed VATSIM flight plan
- departure and arrival airport
- SimBrief navlog data
- report fixes
- ATIS/PDC target logic

It also resets:

- current ATS unit
- clearance state
- PDC state
- ATIS state
- cached ATIS/PDC data
- arrival reminder state

### Arrival reminder

After landing, EasyCPDLC can show a system reminder:

```text
LANDED / ARRIVAL DETECTED. IF A NEW FLIGHT PLAN WITH A DIFFERENT CALLSIGN IS FILED, PRESS RLD FP AFTER ABOUT 5 MINUTES SO EASYCPLC CAN UPDATE.
```

This reminder is intended for multi-leg flying. VATSIM data may take a few minutes to update after filing a new flight plan, so waiting briefly before pressing **RLD FP** helps EasyCPDLC load the correct new session data.

---

## Smart message handling

The modernized message overview improves readability by converting common datalink responses into shorter cockpit-style summaries.

Examples:

- `REQUESTING ATIS FOR LOWW`
- `LOWW ATIS E RECEIVED QNH 1012 RWY 29`
- `REQUESTING METAR FOR LOWW`
- `LOWW METAR RECEIVED QNH 1012 WIND 290/08 RWY 29`
- `ATIS NOT AVAILABLE`

ATIS responses try to detect the current ATIS information letter and display it directly in the message list.

---

## Status indicators

The main DCDU page includes compact status indicators below the current ATC unit area.

### CLR

The CLR indicator shows the current clearance state:

- white = neutral / no active clearance state
- orange = requested / standby
- green = received or accepted
- red = rejected

### PDC

The PDC indicator shows whether a matching Hoppie CPDLC/PDC station appears to be available:

- white = unknown / not checked yet
- green = online / available
- red = offline / unavailable

After the aircraft has been airborne, EasyCPDLC switches status targeting from the departure airport to the arrival airport. This prevents the old departure airport from incorrectly keeping PDC green after arrival.

### ATIS

The ATIS indicator shows whether ATIS data is available for the currently relevant airport.

- before departure: departure airport
- airborne / after departure: arrival airport
- after landing: arrival airport until **RLD FP** is used

---

## ATIS, METAR and automatic arrival logic

METAR and ATIS requests are aware of the flight phase.

- On the ground before departure, the suggested airport is the **departure ICAO**.
- After becoming airborne, the suggested airport changes to the **arrival ICAO**.
- If the field still contains the departure ICAO while airborne, EasyCPDLC automatically replaces it with the arrival ICAO when sending.
- Manually entered alternate ICAOs are not overwritten.

This is intended to reduce wrong ATIS/METAR requests during arrival and multi-leg operations.

---

## Free text cooldown

To reduce accidental or excessive free-text usage, free-text messages use a cooldown system.

By default, free text can only be sent once every 5 minutes.

If free text is attempted too early, EasyCPDLC shows a system message such as:

```text
FREE TEXT AVAILABLE IN 04:32
```

METAR and ATIS requests are not affected by this cooldown.

---

## Message filtering

The message overview can be filtered by message type.

Available filters include:

- ALL
- NEW
- ATIS
- METAR
- CPDLC
- TELEX
- SYSTEM

This makes it easier to keep the DCDU overview readable during busy flights.

---

## Requirements

- Windows
- .NET 10 Desktop Runtime
- VATSIM account and CID
- Hoppie ACARS logon code
- Active internet connection

> Note: the .NET Desktop Runtime may already be included in packaged builds, depending on the release.

---

## Installation

### Download a release

1. Download the latest release from the repository's **Releases** page.
2. Extract the ZIP file to a folder of your choice.
3. Start `EasyCPDLC.exe`.
4. Enter your Hoppie logon code and VATSIM CID.
5. Click **Connect**.

---

## First start

1. Open EasyCPDLC.
2. Enter your **Hoppie logon code**.
3. Enter your **VATSIM CID**.
4. Enable **Remember Me** if you want the client to save your login details locally.
5. Press **Connect**.
6. Use the DCDU panel to access CPDLC, TELEX, ATC, and setup functions.
7. On the setup page, choose between Boeing and Airbus style.
8. Add your SimBrief Pilot ID if you want SimBrief navlog integration.
9. Optional: create a Flow Pro widget using the [Flow Pro setup](#flow-pro-setup) section.

---

## Project status

This is a community-maintained modernization fork. The main goals are:

- keeping EasyCPDLC compatible with modern .NET versions
- improving the user interface
- making CPDLC more comfortable for day-to-day VATSIM flying
- improving MSFS cockpit workflow with Flow Pro and tray integration

---

## Credits

Original project:

**EasyCPDLC**  
Copyright (C) 2022 Joshua Seagrave

This project is based on the original **EasyCPDLC** project by **quassbutreally**:  
https://github.com/quassbutreally/EasyCPDLC

Thanks to the VATSIM and Hoppie communities for making realistic datalink simulation possible for online pilots.

---

## Disclaimer

EasyCPDLC is a third-party community tool for flight simulation. It is not affiliated with, endorsed by, or officially connected to VATSIM, Hoppie, aircraft manufacturers, or real-world aviation authorities.

This software is provided as is, without warranty of any kind, express or implied.

The authors and contributors are not responsible or liable for any damages, data loss, connection issues, network disruptions, incorrect messages, missed ATC instructions, software crashes, simulator issues, or any other problems resulting from the use or inability to use this software.

This project is intended for flight simulation use only.

It must not be used for real-world aviation, real-world communication, real-world navigation, operational flight planning, or any safety-critical purpose.

Use this software at your own risk.

This project is an unofficial community modification/fork and is not affiliated with, endorsed by, or officially supported by VATSIM, the Hoppie ACARS network, or the original EasyCPDLC author.

---

## License

This project is licensed under the GNU General Public License v3.0 or later.
