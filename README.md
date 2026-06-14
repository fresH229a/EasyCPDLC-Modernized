# EasyCPDLC – Modernized

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![Network](https://img.shields.io/badge/network-VATSIM%20%2B%20Hoppie-blue)
![Status](https://img.shields.io/badge/status-community%20fork-orange)

A modernized and visually refreshed fork of **EasyCPDLC**, the lightweight CPDLC client for pilots flying on **VATSIM** via the **Hoppie ACARS** network.

This build updates the project for **.NET 10** and introduces a redesigned cockpit-style interface with cleaner DCDU layouts, updated controls, and a more polished login experience.

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

After connecting, EasyCPDLC can be used for common datalink workflows such as ATC logon, clearance requests, TELEX-style messages, and CPDLC communication during online flights.

---

## What changed in this fork?

This fork focuses on modernization, compatibility, and visual polish.

### Highlights

- Updated for **.NET 10**
- Refreshed login screen with a cleaner VATSIM/Hoppie connection flow
- Redesigned DCDU-style interface
- Airbus-inspired and Boeing-inspired panel variants
- Improved button styling, spacing, shadows, and cockpit-like visual hierarchy
- Redesigned CPDLC, ATC, TELEX, METAR, and ATIS screens
- Smart message overview for weather and clearance messages
- Status indicators for clearance, PDC availability, and ATIS availability
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

---

## Smart message handling

The modernized message overview improves readability by converting common datalink responses into shorter cockpit-style summaries.

Examples:

- REQUESTING ATIS FOR LOWW
- LOWW ATIS E RECEIVED QNH 1012 RWY 29
- REQUESTING METAR FOR LOWW
- LOWW METAR RECEIVED QNH 1012 WIND 290/08 RWY 29
- ATIS NOT AVAILABLE

ATIS responses try to detect the current ATIS information letter and display it directly in the message list.

---

## Status indicators

The main DCDU page includes compact status indicators below the current ATC unit area.

### CLR
The CLR indicator shows the current clearance state:

- white = neutral / clearance received
- orange = requested / standby
- green = accepted
- red = rejected

### PDC
The PDC indicator shows whether a matching Hoppie CPDLC/PDC station appears to be available:

- white = unknown / not checked yet
- green = online / available
- red = offline / unavailable
- ATIS

### ATIS
The ATIS indicator shows whether arrival and/or departure ATIS data is available for the current airport when available from VATSIM data.

---

## Free text cooldown

To reduce accidental or excessive free-text usage, free-text messages use a cooldown system.
By default, free text can only be sent once every 5 minutes.
If free text is attempted too early, EasyCPDLC shows a system message such as:

FREE TEXT AVAILABLE IN 04:32

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
- .NET 10 Desktop Runtime (its included in the build)
- VATSIM account and CID
- Hoppie ACARS logon code
- Active internet connection

---

## Installation

### Download a release

1. Download the latest release from the repository's **Releases** page.
2. Extract the ZIP file to a folder of your choice.
3. Start `EasyCPDLC.exe`.
4. Enter your Hoppie logon code and VATSIM CID.
5. Click **Connect**.



## First start

1. Open EasyCPDLC.
2. Enter your **Hoppie logon code**.
3. Enter your **VATSIM CID**.
4. Enable **Remember Me** if you want the client to save your login details locally.
5. Press **Connect**.
6. Use the DCDU panel to access CPDLC, TELEX, ATC, and setup functions.
7. On the Setup Page you can choose between Boeing and Airbus Style and add your Simbrief Pilot ID

---

## Project status

This is a community-maintained modernization fork. The main goals are:

- keeping EasyCPDLC compatible with modern .NET versions
- improving the user interface
- making CPDLC more comfortable for day-to-day VATSIM flying

---


## Credits

Original project:
EasyCPDLC
Copyright (C) 2022 Joshua Seagrave

This project is based on the original **EasyCPDLC** project by **quassbutreally**. https://github.com/quassbutreally/EasyCPDLC

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