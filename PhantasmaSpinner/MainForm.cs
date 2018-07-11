using Phantasma.Consensus;
using Phantasma.Core;
using Phantasma.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhantasmaSpinner
{
    public partial class MainForm : Form
    {
        public const int basePort = 7060;

        private List<Node> _nodes = new List<Node>();
        private List<Endpoint> _seeds = new List<Endpoint>();

        private int currentLogSize;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var nodeCount = 3;


            for (int i = 0; i < nodeCount; i++)
            {
                _seeds.Add(new Endpoint("127.0.0.1", basePort + i));
            }

            for (int i = 0; i < nodeCount; i++)
            {
                var log = new CustomLogger();
                log.Message("Spinning node #" + i);

                var keys = KeyPair.Random();
                var port = basePort + i;
                var node = new Node(keys, port, _seeds, log);
                _nodes.Add(node);

                listBox1.Items.Add("Node #" + i);
            }

            foreach (var node in _nodes) {
                node.Start();
            }

            ShowLogForNode(_nodes[0]);
        }

        private void ShowLogForNode(Node node) {

            var log = (CustomLogger)node.Log;
            var sb = new StringBuilder();

            for (int i=currentLogSize; i<log.entries.Count; i++)
            {
                var entry = log.entries[i];
                sb.AppendLine(entry.message);
            }
            textBox1.Text = sb.ToString();
            currentLogSize = log.entries.Count;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            var node = _nodes[listBox1.SelectedIndex];
            currentLogSize = 0;
            textBox1.Clear();
            ShowLogForNode(node);        
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var node = _nodes[listBox1.SelectedIndex];
            var log = (CustomLogger)node.Log;
            if (currentLogSize < log.entries.Count) {
                ShowLogForNode(node);
            }
        }
    }
}
