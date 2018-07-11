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

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < 3; i++) {
                var log = new CustomLogger();
                log.Message("Spinning node #" + i);

                var keys = KeyPair.Random();
                var port = basePort + i;
                var node = new Node(keys, port, _seeds, log);
                node.Start();
                _nodes.Add(node);

                listBox1.Items.Add("Node #" + i);
            }

            ShowLogForNode(_nodes[0]);
        }

        private void ShowLogForNode(Node node) {
            textBox1.Clear();

            var log = (CustomLogger)node.Log;
            var sb = new StringBuilder();
            foreach (var entry in log.entries)
            {
                sb.AppendLine(entry.message);
            }
            textBox1.Text = sb.ToString();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        
            var node = _nodes[listBox1.SelectedIndex];
            ShowLogForNode(node);        
        }

    }
}
