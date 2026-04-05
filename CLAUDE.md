# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Windows Forms (.NET 8) application that bridges a **Hercules DJControl Starlight** MIDI controller to a **FlexRadio SDR** (Software Defined Radio). MIDI knob/button events are translated into radio commands (tune, filter, mode, PTT, etc.).

## Build

Build using Visual Studio or MSBuild. The project requires:
- Windows (targets `net8.0-windows`, uses WinForms)
- FlexLib local project references at `../FlexRadio/FlexLib_API_v4.1.5.39794/` (FlexLib, UiWpfFramework, Util, Vita)
- NuGet: `Melanchall.DryWetMidi` v8.0.2

```
dotnet build MidiFlexRadioController.sln
dotnet build MidiFlexRadioController.sln -c Release -p:Platform=x64
```

There are no automated tests in this project.

## Architecture

The app has four main classes wired together in `MainForm`:

```
MidiController  ──CommandHandler──►  Transceiver
     ▲                                    │
     │  CommandStateEvent / TXStateEvent  │
     └────────────────────────────────────┘
```

- **`MidiController`** (`MidiController.cs`) — owns the DryWetMidi `InputDevice`/`OutputDevice`. Hard-coded to device name `"DJControl Starlight"`. Contains two static dictionaries mapping `MidiControl(channel, number)` → `RadioAction`: `noteMapping` (buttons/NoteOn) and `ccMapping` (knobs/ControlChange). Fires `CommandHandler` events on input; exposes `LightSliceTX` and `LightActionButton` to drive LED feedback on the controller.

- **`Transceiver`** (`Transceiver.cs`) — wraps the FlexLib `API`. Discovers radios on the network, auto-connects when a matching GUI client station name appears. Station name matching checks `COMPUTERNAME`, the `SMARTSDR-STATION-NAME` env var, and `"AetherSDR"`. `ProcessCommand` is the central dispatch that translates `ControlCommand` records into FlexLib `Slice`, `Panadapter`, and `Radio` API calls. Supports slices `"A"`, `"B"`, or `BOTH_SLICES`.

- **`TrxEventsHandler`** (`TrxEventsHandler.cs`) — subscribes to FlexLib radio/slice/panadapter property change events and forwards relevant state (MOX/TX, ATU status, slice toggles) back to `MainForm`/`MidiController` via delegate callbacks so LED lights stay in sync.

- **`ControlCommand.cs`** — defines the shared data model: `Command` enum, `RadioAction(TrxCommand, Param)`, `MidiControl(Channel, Number)`, `MidiEvent(Channel, Number, Value)`, `ControlCommand(Action, MidiEvent)`.

## Key Behaviours

- MIDI knob values are 0–127 (relative encoder, center=64). `GetKnobPosition` normalises these.
- Slice `"B"` volume/AGC knob falls back to slice `"D"` (diversity) if `"B"` is not found.
- Commands like `APF_ANF`, `DVK`, `FilterWidth/Shift`, `RitXit`, and `Step` are mode-aware (CW vs SSB).
- When slice `"B"` is added, the diversity slice `"D"` is automatically muted; unmuted when `"B"` is removed.
- DVK playback uses CWX macros in CW mode and DVK recordings in SSB mode; pressing again cancels.

## Configuration

- MIDI device name is hard-coded in `MainForm.cs:19`: `midiController.Setup("DJControl Starlight")`
- SmartSDR station name can be overridden via environment variable `SMARTSDR-STATION-NAME`
