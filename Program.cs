namespace MidiFlexRadioController
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            using var mutex = new System.Threading.Mutex(true, "MidiFlexRadioController_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Another instance of this program is already running.", "MidiFlexRadioController", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}