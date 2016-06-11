using HomerAutoTunerApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HomerAutoTunerApp
{
    public partial class Form1 : Form
    {
        HomerAutoTuner m_tuner;
        int stub1Pos;
        int stub2Pos;
        int stub3Pos;
        int m_stepSize = 10;
        public Form1()
        {
            InitializeComponent();
            try
            {
                comboBox1.SelectedIndex = 0;
                numericUpDown1.Maximum = (decimal)22.7;
                numericUpDown2.Maximum = (decimal)22.7;
                numericUpDown3.Maximum = (decimal)22.7;

                numericUpDown1.Increment = 1;
                numericUpDown2.Increment = 1;
                numericUpDown3.Increment = 1;
             
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }
        void loadSetting()
        {
            textBox1.Text = Properties.Settings.Default.comport;
        }
        void saveSettings()
        {
            Properties.Settings.Default.comport = textBox1.Text;
            Properties.Settings.Default.Save();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (m_tuner == null)
                {
                    m_tuner = new HomerAutoTuner("COM4", 115200);
                    m_tuner.Connect();
                    groupBox1.Enabled = true;
                }
                else
                {
                    m_tuner.Close();
                    groupBox1.Enabled = false;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                stub1Pos = (int)numericUpDown1.Value * m_stepSize;
                m_tuner.SetMotorPositions(stub1Pos, stub2Pos, stub3Pos);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                stub2Pos = (int)numericUpDown2.Value * m_stepSize;
                m_tuner.SetMotorPositions(stub1Pos, stub2Pos, stub3Pos);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                
                stub3Pos = (int)numericUpDown3.Value * m_stepSize;
                m_tuner.SetMotorPositions(stub1Pos, stub2Pos, stub3Pos);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                m_tuner.AllStubsHome();
                numericUpDown1.Value = 0;
                numericUpDown2.Value = 0;
                numericUpDown3.Value = 0;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (m_tuner != null)
                {
                    m_tuner.AllStubsHome();
                    ReadMotorsPositions();
                }
            }
            catch (Exception err)
            {

            }

            try
            {
                if (m_tuner != null)
                    m_tuner.Close();
            }
            catch (Exception err)
            {

            }
         
        }

        void ReadMotorsPositions()
        {
            ushort stub1;
            ushort stub2;
            ushort stub3;
            m_tuner.ReadMotorPositions(out stub1, out stub2, out stub3);
            label6.Text = stub1.ToString() + " ,  " + stub2.ToString() + " , " + stub3.ToString();
        }
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {

                ReadMotorsPositions();
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
            {
                m_stepSize = 10;
                numericUpDown1.Maximum = (decimal)22.7;
                numericUpDown2.Maximum = (decimal)22.7;
                numericUpDown3.Maximum = (decimal)22.7;
            }
            if (comboBox1.SelectedIndex == 1)
            {
                m_stepSize = 1;
                numericUpDown1.Maximum = 227;
                numericUpDown2.Maximum = 227;
                numericUpDown3.Maximum = 227;
            }
        }
    }
}
