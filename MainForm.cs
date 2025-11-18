
namespace MidiFlexRadioController
{
    public partial class MainForm : Form
    {
        private readonly Transceiver transceiver = new();
        private readonly MidiController midiController = new();
        private bool isRunning = false;

        public MainForm()
        {
            InitializeComponent();
            transceiver.StatusEvent += UpdateStatus;
            transceiver.CommandStateEvent += midiController.LightActionButton;
            transceiver.TXStateEvent += midiController.LightSliceTX;
            midiController.CommandHandler += transceiver.ProcessCommand;
            UpdateStatus(ConnectionStatus.Connecting);
            midiController.Setup("DJControl Starlight");
            transceiver.Setup();
        }

        private void Transceiver_TXStateEvent(string sliece, bool isTX)
        {
            throw new NotImplementedException();
        }

        private void UpdateStatus(ConnectionStatus status)
        {
            isRunning = status == ConnectionStatus.Connected || status == ConnectionStatus.Connecting;
            if (InvokeRequired)
            {
                Invoke(() => UpdateStatus(status));
                return;
            }
            StatusLabel.Text = status.ToString();
            StatusLabel.ForeColor = status == ConnectionStatus.ConnectionFailed ? Color.Red
                : status == ConnectionStatus.Connected ? Color.Green
                : Color.Black;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var x = MessageBox.Show("Are you sure you want to really exit ?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            e.Cancel = x == DialogResult.No;
            if (x == DialogResult.Yes)
            {
                midiController.Teardown();
                transceiver.Teardown();
            }
        }
    }
}
