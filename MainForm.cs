
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
            UpdateStatus(new ConnectionInfo(ConnectionStatus.Connecting, ""));
            midiController.Setup("DJControl Starlight");
            transceiver.Setup();
        }

        private void UpdateStatus(ConnectionInfo connectionInfo)
        {
            var status = connectionInfo.Status;
            isRunning = status == ConnectionStatus.Connected || status == ConnectionStatus.Connecting;
            if (InvokeRequired)
            {
                Invoke(() => UpdateStatus(connectionInfo));
                return;
            }
            statusLabel.Text = status.ToString();
            statusLabel.ForeColor = status == ConnectionStatus.ConnectionFailed ? Color.Red
                : status == ConnectionStatus.Connected ? Color.Green
                : Color.Black;
            radioLabel.Text = connectionInfo.RadioLabel;

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var x = MessageBox.Show("Do you want to stop FlexRadio Midi Controller?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            e.Cancel = x == DialogResult.No;
            if (x == DialogResult.Yes)
            {
                transceiver.Teardown();
                midiController.Teardown();                
            }
        }
    }
}
