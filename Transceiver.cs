using Flex.Smoothlake.FlexLib;
using System.Diagnostics;
using System.Windows;


namespace MidiFlexRadioController
{
    internal record ConnectionInfo(ConnectionStatus Status, string RadioLabel);
    enum ConnectionStatus
    {
        Connected,
        Disconnected,
        Connecting,
        ConnectionFailed,
        Disconnecting
    }

    internal class Transceiver
    {
        private readonly object _connectSyncObj = new();
        private readonly List<string> stationNames = [];

        private readonly List<int> BANDS = [.. new List<int>() { 160, 80, 40, 30, 20, 17, 15, 12, 10, 6 }.OrderBy(x => x)];
        private readonly List<string> BASE_MODES = new List<string>() { "CW", "LSB", "USB" };
        private readonly List<Command> BASE_MODES_ONLY_COMMANDS = new List<Command>()
        {
            Command.APF_ANF,
            Command.DVK,
            Command.FilterNarrower,
            Command.FilterWider,
            Command.FilterWidth,
            Command.FilterShift,
            Command.RitXit,
            Command.Step,
            Command.XitOnOff,
        };

        private readonly List<int> CW_FILTERS_WIDTH = [.. new List<int>() { 200, 300, 400, 500, 600 }.OrderBy(x => x)];
        private readonly int CW_FILTER_DEFAULT_HZ = 400;
        private readonly int CW_FILTER_VARIABLE_DELTA_HZ = 200;
        private readonly List<int> CW_TUNE_STEPS = [.. new List<int>() { 5, 50 }.OrderBy(x => x)];

        private readonly List<int> SSB_FILTERS_WIDTH = [.. new List<int>() { 1900, 2100, 2400, 2700 }.OrderBy(x => x)];
        private readonly int SSB_FILTER_LOW_HZ = 100;
        private readonly int SSB_FILTER_DEFAULT_HZ = 2100;
        private readonly int SSB_FILTER_VARIABLE_DELTA_HZ = 400;
        private readonly List<int> SSB_TUNE_STEPS = [.. new List<int>() { 20, 200 }.OrderBy(x => x)];

        private readonly List<Radio> radios = [];
        private Radio? activeRadio = null;
        private TrxEventsHandler? radioHandler;

        public delegate void StatusEventHandler(ConnectionInfo connectionInfo);
        public event StatusEventHandler? StatusEvent;
        public delegate void CommandStateEventHandler(RadioAction action, bool on);
        public event CommandStateEventHandler? CommandStateEvent;
        public delegate void TXStateEventHandler(string sliece, bool? isTX);
        public event TXStateEventHandler? TXStateEvent;

        // init station names in constructor
        internal Transceiver()
        {
            var localCompName = System.Environment.GetEnvironmentVariable("COMPUTERNAME");
            if (localCompName != null)
            {
                stationNames.Add(localCompName);
            }
            var customStationName = System.Environment.GetEnvironmentVariable("SMARTSDR-STATION-NAME");
            if (customStationName != null)
            {
                stationNames.Add(customStationName);
            }
        }

        internal void Setup()
        {
            API.RadioAdded += API_RadioAdded;
            API.RadioRemoved += API_RadioRemoved;
            API.ProgramName = "MidiFlexRadioController";
            API.Init();
        }

        internal void Teardown()
        {
            radioHandler?.Teardown();
            API.RadioAdded -= API_RadioAdded;
            API.RadioRemoved -= API_RadioRemoved;
            foreach (var r in radios)
            {
                r.Disconnect();
            }
            radios.Clear();
            API.CloseSession();
        }

        public bool IsConnected()
        {
            return activeRadio != null && activeRadio.Connected;
        }

        public void ProcessCommand(ControlCommand controlCommand)
        {
            if (IsConnected() == false)
            {
                Debug.WriteLine("WARN: Radio disconected!");
                return;
            }
            var client = activeRadio.GuiClients.FirstOrDefault();
            if (client == null)
            {
                Debug.WriteLine("WARN: client is null!");
                return;
            }
            var actionParam = controlCommand.Action.Param;
            var trxCommand = controlCommand.Action.TrxCommand;
            if (actionParam == MidiController.BOTH_SLICES)
            {
                ProcessCommand(new ControlCommand(new RadioAction(trxCommand, "A"), controlCommand.MidiEvent));
                ProcessCommand(new ControlCommand(new RadioAction(trxCommand, "B"), controlCommand.MidiEvent));
                return;
            }

            Slice? s = null;
            Slice? divSlice = null;
            string? sliceMode = null;
            Panadapter? p = null;
            if (actionParam == "A" || actionParam == "B")
            {
                s = activeRadio.FindSliceByLetter(actionParam, client.ClientHandle);
                if (s == null && actionParam == "B"
                    && (trxCommand == Command.Volume || trxCommand == Command.AgcT))
                {
                    s = activeRadio.FindSliceByLetter("D", client.ClientHandle);
                }
                if (s == null)
                {
                    Debug.WriteLine($"Slice {actionParam} not found, will not execute [{trxCommand}]");
                    return;
                }
                divSlice = s.DiversityOn ? activeRadio.FindSliceByLetter("D", client.ClientHandle) : null;
                p = s.Panadapter;

                sliceMode = s?.DemodMode;
                if (sliceMode == null || (!BASE_MODES.Contains(sliceMode) && BASE_MODES_ONLY_COMMANDS.Contains(trxCommand)))
                {
                    Debug.WriteLine($"[{trxCommand}] cannot be executed for mode {sliceMode}");
                    return;
                }
            }


            switch (trxCommand)
            {
                case Command.Tune:
                    {
                        double step = (s.TuneStep / 1_000_000.0);
                        double newFreq = controlCommand.MidiEvent.Value < 63 ? s.Freq + step : s.Freq - step;
                        s.Freq = newFreq;
                        break;
                    }
                case Command.RitXit:
                    {
                        var ritStep = sliceMode == "CW" ? 10 : 40;
                        if (s.XITOn)
                        {
                            s.XITFreq = - 40 * GetKnobPosition(controlCommand.MidiEvent.Value, 4);
                        }
                        else
                        {
                            s.RITFreq = - ritStep * GetKnobPosition(controlCommand.MidiEvent.Value, 32);
                            if (s.DiversityOn)
                            {
                                activeRadio.FindSliceByIndex(s.DiversityIndex).RITOn = s.RITFreq != 0;
                            }
                            s.RITOn = s.RITFreq != 0;
                        }
                        break;
                    }
                case Command.Volume:
                    {
                        var gain = GetKnobPosition(controlCommand.MidiEvent.Value, 0, 100, 1);
                        s.AudioGain = gain;
                        break;
                    }
                case Command.AgcT:
                    {
                        var threshold = GetKnobPosition(controlCommand.MidiEvent.Value, 5, 70, 1);
                        s.AGCThreshold = threshold;
                        break;
                    }
                case Command.FilterWidth:
                    {
                        var defaultWidth = sliceMode == "CW" ? CW_FILTER_DEFAULT_HZ : SSB_FILTER_DEFAULT_HZ;
                        var widthDelta = sliceMode == "CW" ? CW_FILTER_VARIABLE_DELTA_HZ : SSB_FILTER_VARIABLE_DELTA_HZ;
                        if (sliceMode == "CW")
                        {
                            var halfWidth = defaultWidth / 2;
                            halfWidth += (widthDelta * GetKnobPosition(controlCommand.MidiEvent.Value, 8)) / (2 * 8);
                            s.UpdateFilter(-halfWidth, halfWidth);
                        }
                        else if (sliceMode == "USB")
                        {
                            s.UpdateFilter(SSB_FILTER_LOW_HZ, SSB_FILTER_LOW_HZ + defaultWidth + (widthDelta * GetKnobPosition(controlCommand.MidiEvent.Value, 8)) / 8);
                        }
                        else
                        {
                            s.UpdateFilter(-(SSB_FILTER_LOW_HZ + defaultWidth + (widthDelta * GetKnobPosition(controlCommand.MidiEvent.Value, 8)) / 8), -SSB_FILTER_LOW_HZ);
                        }
                        break;
                    }
                case Command.FilterShift:
                    var shiftMaxHz = sliceMode == "CW" ? 150 : 300;
                    var shiftHz = GetKnobPosition(controlCommand.MidiEvent.Value, -shiftMaxHz, shiftMaxHz, 40);
                    var filterWidth = s.FilterHigh - s.FilterLow;
                    if (sliceMode == "CW")
                    {
                        var halfWidth = filterWidth / 2;
                        s.UpdateFilter(-halfWidth + shiftHz, halfWidth + shiftHz);
                    }
                    else if (sliceMode == "USB" || sliceMode == "LSB")
                    {
                        if (SSB_FILTER_LOW_HZ + shiftHz > 0) { 
                            var filterLow = SSB_FILTER_LOW_HZ + shiftHz;
                            var filterHigh = SSB_FILTER_LOW_HZ + filterWidth + shiftHz;
                            if (sliceMode == "USB")
                            {
                                s.UpdateFilter(filterLow, filterHigh);
                            }
                            else
                            {
                                s.UpdateFilter(-filterHigh, -filterLow);
                            }
                        }
                    }
                    break;
                case Command.ZoomPanadapter:
                    {
                        var level = GetKnobPosition(controlCommand.MidiEvent.Value, 100, 16, 2);
                        p.Bandwidth = level * level / 100_000.0;
                        break;
                    }

                //
                // BUTTONS
                //
                case Command.Diversity:
                    {
                        s.DiversityOn = !s.DiversityOn;
                        CommandStateEvent?.Invoke(controlCommand.Action, s.DiversityOn);
                        break;
                    }
                case Command.CenterPanadapter:
                    {
                        var centerFreq = s.Freq;
                        if (sliceMode== "USB")
                        {
                            centerFreq += s.FilterHigh / 2_000_000.0;
                        }
                        else if (sliceMode == "LSB")
                        {
                            centerFreq += s.FilterLow / 2_000_000.0;
                        }
                        p.CenterFreq = centerFreq;
                        break;
                    }
                case Command.XitOnOff:
                    {
                        s.XITOn = !s.XITOn;
                        break;
                    }
                case Command.APF_ANF:
                    {
                        if (sliceMode == "CW")
                        {
                            s.APFOn = !s.APFOn;
                        } else if (sliceMode == "LSB" || sliceMode == "USB")
                        {
                            s.ANFOn = !s.ANFOn;
                        }
                        break;
                    }
                case Command.Step:
                    {
                        s.TuneStep = FindClosestBiggerValue(s.TuneStep, getModesTuneSteps(sliceMode));
                        break;
                    }
                case Command.FilterWider:
                case Command.FilterNarrower:
                    {
                        var newWidth = trxCommand == Command.FilterWider
                            ? FindClosestBiggerValue(s.FilterHigh - s.FilterLow, getModesFilterWidths(sliceMode), false)
                            : FindClosestSmallerValue(s.FilterHigh - s.FilterLow, getModesFilterWidths(sliceMode), false);
                        if (sliceMode == "CW")
                        {
                            var halfWidth = newWidth / 2;
                            s.UpdateFilter(-halfWidth, halfWidth);
                        }
                        else if (sliceMode == "USB")
                        {
                            s.UpdateFilter(SSB_FILTER_LOW_HZ, newWidth + SSB_FILTER_LOW_HZ);
                        }
                        else
                        {
                            s.UpdateFilter(-(newWidth + SSB_FILTER_LOW_HZ), -SSB_FILTER_LOW_HZ);
                        }
                        break;
                    }
                case Command.DVK:
                    {
                        var dvkNumber = actionParam;
                        if (int.TryParse(dvkNumber, out int dvkIdx) == false)
                        {
                            Debug.WriteLine($"Invalid DVK number: {dvkNumber}");
                            break;
                        }
                        // get active slice mode
                        var txSlice = activeRadio.SliceList.Find(s => s.IsTransmitSlice);
                        if (txSlice == null)
                        {
                            Debug.WriteLine("Transmit slice not found, ignoring DVK command");
                            return;
                        }
                        var txMode = txSlice.DemodMode;
                        var isTX = activeRadio.Mox;
                        if (txMode == "USB" || txMode == "LSB")
                        {
                            if (activeRadio.DVK.Recordings.Find(r => r.Id == dvkIdx && r.DurationMilliseconds > 0) == null)
                            {
                                Debug.WriteLine($"Empty DVK recording: {dvkIdx}");
                                return;
                            }
                            activeRadio.DVK.SendCommand(new DVKCommand(DVKCommandType.StopPlayback, (uint?)dvkIdx, ""));
                            if (!isTX)
                            {
                                activeRadio.DVK.SendCommand(new DVKCommand(DVKCommandType.StartPlayback, (uint?)dvkIdx, ""));
                            }
                        } else if (txMode == "CW")
                        {
                            //do not send message if radio is currently transmitting - cancel request
                            //activeRadio.GetCWX().Macros
                            activeRadio.GetCWX().ClearBuffer();
                            if (!isTX)
                            {
                                activeRadio.GetCWX().SendMacro(dvkIdx);
                            }
                        }
                        break;
                    }
                case Command.Mode:
                    {
                        s.DemodMode = sliceMode == "CW" ? SSBModeFromFreq(s.Freq) : "CW";
                        break;
                    }
                case Command.BandDown:
                    {
                        Int32.TryParse(p.Band, out int currentBand);
                        s.Panadapter.Band = FindClosestBiggerValue(currentBand, BANDS).ToString();
                        break;
                    }
                case Command.BandUp:
                    {
                        Int32.TryParse(p.Band, out int currentBand);
                        s.Panadapter.Band = FindClosestSmallerValue(currentBand, BANDS).ToString();
                        break;
                    }
                case Command.ATU:
                    {
                        if (activeRadio.ATUTuneStatus == ATUTuneStatus.ManualBypass || activeRadio.ATUTuneStatus == ATUTuneStatus.Bypass)
                        {
                            activeRadio.ATUTuneStart();
                            CommandStateEvent?.Invoke(controlCommand.Action, true);
                        } else
                        {
                            activeRadio.ATUTuneBypass();
                            CommandStateEvent?.Invoke(controlCommand.Action, false);
                        }
                        break;
                    }
                case Command.Mute:
                    s.Mute = !s.Mute;
                    break;
                case Command.AudioBalance:
                    if (s.AudioPan == 50)
                    {
                        s.AudioPan = s.Letter == "A" ? 0 : 100;
                    }
                    else
                    {
                        s.AudioPan = 50;
                    }
                    break;
                case Command.WNB:
                    s.WNBOn = !s.WNBOn;
                    break;
                case Command.PTT:
                    activeRadio.Mox = !activeRadio.Mox;
                    break;
                default:
                    Debug.WriteLine($"Unsupported command: {trxCommand}");
                    break;
            }
        }

        private bool isBaseMode(string? mode)
        {
            return mode != null && BASE_MODES.Contains(mode);
        }

        private List<int> getModesFilterWidths(string mode)
        {
            return mode == "CW" ? CW_FILTERS_WIDTH : SSB_FILTERS_WIDTH;
        }

        private List<int> getModesTuneSteps(string mode)
        {
            return mode == "CW" ? CW_TUNE_STEPS : SSB_TUNE_STEPS;
        }

        private static string SSBModeFromFreq(double freq)
        {
            return freq > 10 ? "USB" : "LSB";
        }

        private static int FindClosestBiggerValue(int currentValue, List<int> availableValues)
        {
            return FindClosestBiggerValue(currentValue, availableValues, true);
        }

        private static int FindClosestBiggerValue(int currentValue, List<int> availableValues, bool rotate)
        {
            if (!rotate && currentValue >= availableValues.Last())
            {
                return availableValues.Last();
            }
            return availableValues.SkipWhile(p => p <= currentValue).FirstOrDefault(availableValues.First());
        }

        private static int FindClosestSmallerValue(int currentValue, List<int> availableValues)
        {
            return FindClosestSmallerValue(currentValue, availableValues, true);
        }

        private static int FindClosestSmallerValue(int currentValue, List<int> availableValues, bool rotate)
        {
            if (!rotate && currentValue <= availableValues.First())
            {
                return availableValues.First();
            }
            return availableValues.TakeWhile(p => p < currentValue).LastOrDefault(availableValues.Last());
        }

        private static int GetKnobPosition(int value, int stepsNo)
        {
            // value 0 - 127, center value 64
            if (value == 127)
            {
                value = 128;
            }
            return stepsNo * (value - 64) / 64;
        }
        private static int GetKnobPosition(int value, int left, int right, int stepWidth)
        {
            if (value == 127)
            {
                value = 128;
            }
            var resultNotRounded = left + (right - left) * value / 127;
            return stepWidth * (resultNotRounded / stepWidth);
        }


        private void API_RadioAdded(Radio radio)
        {
            radios.Add(radio);
            Console.WriteLine($"Radio added: {radio.Nickname} {radio.Callsign}");
            foreach (var client in radio.GuiClients)
            {
                string clientInfoMessage = $"Existing GUI Client: {client.Program} {client.Station}";
                Debug.WriteLine(clientInfoMessage);
                if (stationNames.Contains(client.Station))
                {
                    ActivateRadio(radio);
                }

            }
            radio.GUIClientAdded += (client) =>
            {
                string clientInfoMessage = $"New GUI Client added: {client.Program} {client.Station} Radio: {radio.Nickname}";
                Debug.WriteLine(clientInfoMessage);
                if (stationNames.Contains(client.Station))
                {
                    ActivateRadio(radio);
                }
            };
            radio.GUIClientRemoved += (client) =>
            {
                string clientInfoMessage = $"GUI Client removed: {client.Program} {client.Station} Radio: {radio.Nickname}";
                Debug.WriteLine(clientInfoMessage);
                if (stationNames.Contains(client.Station))
                {
                    DeactivateRadio(radio);
                }
            };
        }

        private void API_RadioRemoved(Radio radio)
        {
            radios.Remove(radio);
            Debug.WriteLine($"Radio removed: {radio.Nickname} {radio.Callsign}");
        }

        private void ActivateRadio(Radio radio)
        {
            lock (_connectSyncObj)
            {
                if (activeRadio == null)
                {
                    this.activeRadio = radio;
                    this.radioHandler = new TrxEventsHandler(radio, TXStateEvent, CommandStateEvent);
                    if (radio?.Connect() == true)
                    {
                        this.activeRadio = radio;
                        Debug.WriteLine("Connected to radio {radio}.");
                        StatusEvent?.Invoke(new ConnectionInfo(ConnectionStatus.Connected, $"{radio.Callsign} {radio.Nickname}"));                                                                        
                    }
                    else
                    {
                        Debug.WriteLine("Failed to connect to radio.");
                        StatusEvent?.Invoke(new ConnectionInfo(ConnectionStatus.ConnectionFailed, ""));
                        this.radioHandler.Teardown();
                        this.radioHandler = null;
                        this.activeRadio = null;           
                    }
                }
            }
        }

        private void DeactivateRadio(Radio radio)
        {
            lock (_connectSyncObj)
            {
                if (this.activeRadio == radio)
                {
                    this.activeRadio = null;
                    radio.Disconnect();
                    Debug.WriteLine($"Disconnected from radio {radio.Nickname}.");
                    StatusEvent?.Invoke(new ConnectionInfo(ConnectionStatus.Disconnected, ""));
                }
            }
        }
    }
}
