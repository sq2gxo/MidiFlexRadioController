namespace MidiFlexRadioController
{
    enum Command
    {
        // Controls
        Tune,
        RitXit,
        Volume,
        AgcT,
        FilterWidth,
        FilterShift,
        ZoomPanadapter,

        // Buttons
        Diversity,
        CenterSlice,
        XitOnOff,
        FilterWider,
        FilterNarrower,
        APF,
        AutoNotch,
        NB,
        WNB,
        Step,
        DVK,
        ATU,
        BandUp,
        BandDown,
        Mode,
    }

    internal record RadioAction(Command TrxCommand, string Param);

    internal record MidiControl(int Channel, int Number);

    internal record MidiEvent(int Channel, int Number, int Value);

    internal record ControlCommand(RadioAction Action, MidiEvent MidiEvent);
}

