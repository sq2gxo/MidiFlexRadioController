using Flex.Smoothlake.FlexLib;
using System.Diagnostics;
using static MidiFlexRadioController.Transceiver;

namespace MidiFlexRadioController
{
    internal class TrxEventsHandler
    {
        private readonly Radio radio;
        public event TXStateEventHandler? TxStateEventHandler;
        public event CommandStateEventHandler? CommandStateEventHandler;

        public TrxEventsHandler(Radio radio, TXStateEventHandler? txStateEventHandler, CommandStateEventHandler? CommandStateEventHandler)
        {
            this.radio = radio;
            this.TxStateEventHandler = txStateEventHandler;
            this.CommandStateEventHandler = CommandStateEventHandler;
            radio.SliceAdded += new Radio.SliceAddedEventHandler(Radio_SliceAdded);
            radio.SliceRemoved += new Radio.SliceRemovedEventHandler(Radio_SliceRemoved);
            radio.PanadapterAdded += new Radio.PanadapterAddedEventHandler(Radio_PanadapterAdded);
            radio.PanadapterRemoved += new Radio.PanadapterRemovedEventHandler(Radio_PanadapterRemoved);
            radio.PropertyChanged += Radio_PropertyChanged;
            radio.GetCWX().PropertyChanged += CWX_PropertyChanged;
            radio.DVK.PropertyChanged += CWX_PropertyChanged;

        }

        public void Teardown()
        {
            radio.PropertyChanged -= Radio_PropertyChanged;
            radio.SliceAdded -= Radio_SliceAdded;
            radio.SliceRemoved -= Radio_SliceRemoved;
            radio.PanadapterAdded -= Radio_PanadapterAdded;
            radio.PanadapterRemoved -= Radio_PanadapterRemoved;
            radio.PropertyChanged -= Radio_PropertyChanged;
            radio.GetCWX().PropertyChanged -= CWX_PropertyChanged;
            ResetLights("A");
            ResetLights("B");
        }


        private void Radio_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var freqProps = new List<string>() {"AvgRXCommandkbps", "AvgTXTotalkbps", "AvgRXTotalkbps",  "AvgTXCommandkbps", "GPSSatellitesTracked", "GPSFreqError", "GPSUtcTime", "MeterPacketTotalCount", "GuiClients", "AvgMeterkbps", "AvgTXCommandkbps", "RemoteNetworkQuality", "GPSFreqError", "GPSUtcTime", "GuiClients", "AvgMeterkbps", "NetworkPing", "RemoteNetworkQuality", "GPSFreqError", "GPSUtcTime", "GuiClients", "NetworkPing", "RemoteNetworkQuality", "GPSFreqError", "GPSUtcTime", "MeterPacketTotalCount", "GuiClients" };
            if (freqProps.Contains(e.PropertyName))
            {
                return;
            }
            if (e.PropertyName == "Mox")
            {
                var sliceLetter = radio.SliceList.Find(s => s.IsTransmitSlice)?.Letter;
                bool isTX = radio.Mox;
                Debug.WriteLine($"MOX changed: {isTX} {sliceLetter}");
                if (sliceLetter == "A" || sliceLetter == "B")
                {
                    TxStateEventHandler?.Invoke(sliceLetter, isTX);
                }
            }
            else if (e.PropertyName == "ATUTuneStatus")
            {
                CommandStateEventHandler?.Invoke(new RadioAction(Command.ATU, ""), !(radio.ATUTuneStatus == ATUTuneStatus.ManualBypass || radio.ATUTuneStatus == ATUTuneStatus.Bypass));
            }
            //Debug.WriteLine($"Radio property changed: {e.PropertyName}");
        }

        private void CWX_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"CWX property changed: {e.PropertyName}");
        }


        private void Radio_PanadapterRemoved(Panadapter pan)
        {
            Debug.WriteLine($"Panadapter removed: {pan.CenterFreq} Hz");
        }

        private void Radio_PanadapterAdded(Panadapter pan, Waterfall fall)
        {
            Debug.WriteLine($"Panadapter added: {pan.CenterFreq} Hz");
        }

        private void Radio_SliceRemoved(Slice slc)
        {
            var letter = slc.Letter;
            Debug.WriteLine($"Slice removed: {letter}");
            slc.PropertyChanged -= Slice_PropertyChanged;
            if (letter == "B")
            {
                // unmute diversity slice
                var client = radio.GuiClients.FirstOrDefault();
                if (client != null)
                {
                    var diversitySlice = radio.FindSliceByLetter("D", client.ClientHandle);
                    if (diversitySlice != null)
                    {
                        diversitySlice.Mute = false; ;
                    }
                }
            }
            if (letter == "A" || letter == "B")
            {
                ResetLights(letter);
            }
            TxStateEventHandler?.Invoke(slc.Letter, null);
        }

        private void Radio_SliceAdded(Slice slc)
        {
            slc.PropertyChanged += Slice_PropertyChanged;
            Debug.WriteLine($"Slice added: {slc.Letter}");
            if (slc.Letter == "B")
            {
                // mute diversity slice
                var client = radio.GuiClients.FirstOrDefault();
                if (client != null)
                {
                    var diversitySlice = radio.FindSliceByLetter("D", client.ClientHandle);
                    if (diversitySlice != null)
                    {
                        diversitySlice.Mute = true;
                    }
                }
            }
            if (slc.Letter == "A" || slc.Letter == "B")
            {
                SetLights(slc);
            }
            TxStateEventHandler?.Invoke(slc.Letter, false);
        }

        private void Slice_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender != null && sender is Slice)
            {
                Slice s = sender as Slice;
                Debug.WriteLine(e.PropertyName);
                Debug.WriteLine(s.Letter);
                // TODO - mute/unmute D if B was unmuted/muted
                switch (e.PropertyName)
                    {
                    case "Mute":
                        if (s.Letter == "B")
                        {
                            // unmute diversity slice
                            var client = radio.GuiClients.FirstOrDefault();
                            if (client != null)
                            {
                                var diversitySlice = radio.FindSliceByLetter("D", client.ClientHandle);
                                if (diversitySlice != null)
                                {
                                    diversitySlice.Mute = !s.Mute;
                                    var mainSlice = radio.FindSliceByLetter("A", client.ClientHandle);
                                    mainSlice.AudioPan = 0;

                                }
                            }
                        }
                        CommandStateEventHandler?.Invoke(new RadioAction(Command.Mute, s.Letter), s.Mute);
                        break;
                    case "XITOn":
                        CommandStateEventHandler?.Invoke(new RadioAction(Command.XitOnOff, s.Letter), s.XITOn);
                        break;
                    case "ANFOn":
                        CommandStateEventHandler?.Invoke(new RadioAction(Command.APF_ANF, s.Letter), s.ANFOn);
                        break;
                    case "APFOn":
                        CommandStateEventHandler?.Invoke(new RadioAction(Command.APF_ANF, s.Letter), s.APFOn);
                        break;
                    case "WNBOn":
                        CommandStateEventHandler?.Invoke(new RadioAction(Command.WNB, s.Letter), s.WNBOn);
                        break;
                    case "AudioPan":
                        CommandStateEventHandler?.Invoke(new RadioAction(Command.AudioBalance, s.Letter), (s.AudioPan != 50));
                        break;
                    default:
                        break;
                    }
            }
        }

        private void ResetLights(String sliceLetter)
        {
            CommandStateEventHandler?.Invoke(new RadioAction(Command.Mute, sliceLetter), false);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.XitOnOff, sliceLetter), false);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.APF_ANF, sliceLetter), false);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.WNB, sliceLetter), false);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.ATU, ""), false);
        }

        private void SetLights(Slice slice)
        {
            var sliceLetter = slice.Letter;
            CommandStateEventHandler?.Invoke(new RadioAction(Command.Mute, sliceLetter), slice.Mute);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.XitOnOff, sliceLetter), slice.XITOn);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.APF_ANF, sliceLetter), slice.APFOn || slice.ANFOn);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.WNB, sliceLetter), slice.WNBOn);
            CommandStateEventHandler?.Invoke(new RadioAction(Command.ATU, ""), !(radio.ATUTuneStatus == ATUTuneStatus.ManualBypass || radio.ATUTuneStatus == ATUTuneStatus.Bypass));
        }

    }
}
