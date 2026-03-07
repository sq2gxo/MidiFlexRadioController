using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using System.Diagnostics;

namespace MidiFlexRadioController
{
    internal class MidiController
    {
        private InputDevice? inputDevice;
        private OutputDevice? outputDevice;

        public static readonly string BOTH_SLICES = "Both";

        private readonly Dictionary<MidiControl, RadioAction> noteMapping = new()
        {
            { new MidiControl(1, 5), new RadioAction(Command.XitOnOff, "A") },
            { new MidiControl(2, 5), new RadioAction(Command.XitOnOff, "B") },
            { new MidiControl(1, 6), new RadioAction(Command.Diversity, "A") },
            { new MidiControl(2, 6), new RadioAction(Command.ATU, "") },
            { new MidiControl(1, 7), new RadioAction(Command.CenterPanadapter, "A") },
            { new MidiControl(2, 7), new RadioAction(Command.CenterPanadapter, "B") },

            { new MidiControl(6, 0), new RadioAction(Command.FilterNarrower, "A") }, // (1) 1 button (HOT CUE active)
            { new MidiControl(6, 1), new RadioAction(Command.FilterWider, "A") },    // (1) 2 button (HOT CUE active)
            { new MidiControl(6, 2), new RadioAction(Command.APF_ANF, "A") },        // (1) 3 button (HOT CUE active)    
            { new MidiControl(6, 3), new RadioAction(Command.WNB, "A") },           // (1) 4 button (HOT CUE active)
            { new MidiControl(7, 0), new RadioAction(Command.FilterNarrower, "B") }, // (2) 1 button (HOT CUE active)
            { new MidiControl(7, 1), new RadioAction(Command.FilterWider, "B") },    // (2) 2 button (HOT CUE active)
            { new MidiControl(7, 2), new RadioAction(Command.APF_ANF, "B") },        // (2) 3 button (HOT CUE active)
            { new MidiControl(7, 3), new RadioAction(Command.WNB, "B") },           // (2) 4 button (HOT CUE active)

            { new MidiControl(6, 16), new RadioAction(Command.AudioBalance, "A") }, // (1) 1 button (LOOP active)
            { new MidiControl(6, 17), new RadioAction(Command.Mute, "A") }, // (1) 2 button (LOOP active)
            { new MidiControl(6, 18), new RadioAction(Command.PTT, "") }, // (1) 3 button (LOOP active)
            { new MidiControl(6, 19), new RadioAction(Command.DVK, "1") },// (1) 4 button (LOOP active)
            { new MidiControl(7, 16), new RadioAction(Command.AudioBalance, "B") }, // (2) 1 button (LOOP active)
            { new MidiControl(7, 17), new RadioAction(Command.Mute, "B") }, // (2) 2 button (LOOP active)
            { new MidiControl(7, 18), new RadioAction(Command.DVK, "2") }, // (2) 3 button (LOOP active)
            { new MidiControl(7, 19), new RadioAction(Command.DVK, "4") },// (2) 4 button (LOOP active)

            { new MidiControl(1, 3), new RadioAction(Command.Mode, BOTH_SLICES) }, // VINYL button - Mode change for both slices
            { new MidiControl(1, 12), new RadioAction(Command.BandUp, "A") }, // left "headphones" button
            { new MidiControl(0, 3), new RadioAction(Command.BandDown, "A") }, // SHIFT button
            { new MidiControl(2, 12), new RadioAction(Command.Step, BOTH_SLICES) },   // right "headphones" button            
        };

        private readonly Dictionary<RadioAction, List<MidiControl>> commandToNotes;

        private readonly Dictionary<MidiControl, RadioAction> ccMapping = new()
        {
            { new MidiControl(1, 10), new RadioAction(Command.Tune, "A") },
            { new MidiControl(2, 10), new RadioAction(Command.Tune, "B") },
            { new MidiControl(1, 8), new RadioAction(Command.RitXit, "A") },
            { new MidiControl(2, 8), new RadioAction(Command.RitXit, "B") },
            { new MidiControl(1, 0), new RadioAction(Command.Volume, "A") },
            { new MidiControl(2, 0), new RadioAction(Command.Volume, "B") },
            { new MidiControl(0, 3), new RadioAction(Command.AgcT, "A") },
            { new MidiControl(0, 4), new RadioAction(Command.AgcT, "B") },
            { new MidiControl(1, 2), new RadioAction(Command.FilterWidth, "A") },
            { new MidiControl(2, 2), new RadioAction(Command.FilterWidth, "B") },
            { new MidiControl(1, 1), new RadioAction(Command.FilterShift, "A") },
            { new MidiControl(2, 1), new RadioAction(Command.FilterShift, "B") },
            { new MidiControl(0, 0), new RadioAction(Command.ZoomPanadapter, "A") },
        };

        public delegate void ControlCommandEventHandler(ControlCommand command);
        public event ControlCommandEventHandler? CommandHandler;

        internal MidiController()
        {
            commandToNotes = noteMapping
                .GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToList());
        }

        internal void Setup(string deviceName)
        {
            inputDevice = InputDevice.GetByName(deviceName);
            if (inputDevice == null)
            {
                throw new Exception($"Input MIDI device '{deviceName}' not found.");
            }
            inputDevice.EventReceived += OnMidiEventReceived;
            inputDevice.StartEventsListening();
            outputDevice = OutputDevice.GetByName(deviceName);
            if (outputDevice == null)
            {
                throw new Exception($"Output MIDI device '{deviceName}' not found.");
            }
            // 4 green, 32 red
            outputDevice.SendEvent(new NoteOnEvent((SevenBitNumber)35, (SevenBitNumber)0) { Channel = (FourBitNumber)1 });
            outputDevice.SendEvent(new NoteOnEvent((SevenBitNumber)35, (SevenBitNumber)0) { Channel = (FourBitNumber)2 });
        }

        internal void Teardown()
        {
            if (inputDevice != null)
            {
                inputDevice.EventReceived -= OnMidiEventReceived;
                inputDevice.StopEventsListening();
                (inputDevice as IDisposable)?.Dispose();
                inputDevice = null;
            }
            if (outputDevice != null)
            {
                LightSliceTX("A", null);
                LightSliceTX("B", null);
                (outputDevice as IDisposable)?.Dispose();
                outputDevice = null;
            }
        }

        private void OnMidiEventReceived(object? sender, MidiEventReceivedEventArgs e)
        {
            MidiEventType eventType = e.Event.EventType;
            if (e.Event.EventType == MidiEventType.ControlChange)
            {
                ControlChangeEvent cc = (ControlChangeEvent)e.Event;
                if (ccMapping.TryGetValue(new MidiControl(cc.Channel, cc.ControlNumber), out RadioAction? action))
                {
                    CommandHandler?.Invoke(new ControlCommand(action, new MidiEvent(cc.Channel, cc.ControlNumber, cc.ControlValue)));
                }
                else
                {
                    Debug.WriteLine($"No mapping found for Control Change event on channel {cc.Channel}, control number {cc.ControlNumber}, value: {cc.ControlValue},  name: {cc.GetControlName}");
                }
            }
            else if (e.Event.EventType == MidiEventType.NoteOn)
            {
                NoteOnEvent noteOn = (NoteOnEvent)e.Event;
                if (noteMapping.TryGetValue(new MidiControl(noteOn.Channel, noteOn.NoteNumber), out RadioAction? action))
                {
                    var midiEvent = new MidiEvent(noteOn.Channel, noteOn.NoteNumber, noteOn.Velocity);
                    LightButton(midiEvent);
                    if (midiEvent.Value < 63)
                    {
                        CommandHandler?.Invoke(new ControlCommand(action, midiEvent));
                    } else if (action.TrxCommand == Command.PTT) // handle ptt as momentary button
                    {
                        CommandHandler?.Invoke(new ControlCommand(action, midiEvent));
                    }
                }
                else
                {
                    Debug.WriteLine($"No mapping found for Note On event on channel {noteOn.Channel}, note number {noteOn.NoteNumber}, value: {noteOn.Velocity}");
                }
            } else
            {
                Debug.WriteLine($"Unhandled MIDI event type: {eventType}");
            }

        }

        public void LightSliceTX(string slice, bool? on)
        {
            int color = 0;
            if (on != null)
            {
                color = on == true ? 32 : 4;
            }
            if (slice == "A")
            {
                outputDevice?.SendEvent(new NoteOnEvent((SevenBitNumber)35, (SevenBitNumber)color) { Channel = (FourBitNumber)1 });                
            }
            else if (slice == "B")
            {
                outputDevice?.SendEvent(new NoteOnEvent((SevenBitNumber)35, (SevenBitNumber)color) { Channel = (FourBitNumber)2 });
            }
        }

        public void LightActionButton(RadioAction action, bool on)
        {
            if (commandToNotes.TryGetValue(action, out List<MidiControl>? buttons))
            {
                foreach (var b in buttons)
                {
                    LightButton(b, on);
                }                
            }
        }

        private void LightButton(MidiControl button, bool on)
        {
            outputDevice?.SendEvent(new NoteOnEvent((SevenBitNumber)button.Number, on ? SevenBitNumber.MaxValue : SevenBitNumber.MinValue) { Channel = (FourBitNumber)button.Channel });
        }

        private void LightButton(MidiEvent midiEvent)
        {
            outputDevice?.SendEvent(new NoteOnEvent((SevenBitNumber)midiEvent.Number, (SevenBitNumber)midiEvent.Value) { Channel = (FourBitNumber)midiEvent.Channel });
        }
    }
}
