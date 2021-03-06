﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace WoWHeadParser
{
    public partial class WoWHeadParserForm : Form
    {
        private DateTime _startTime;
        private Parser _parser = null;
        private Worker _worker = null;
        private List<uint> _entries = null;
        private WelfCreator _creator = null;

        public WoWHeadParserForm()
        {
            InitializeComponent();
            Initial();
        }

        public void Initial()
        {
            Type[] Types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in Types)
            {
                if (type.IsSubclassOf(typeof(Parser)))
                    parserBox.Items.Add(type);
            }

            DirectoryInfo info = new DirectoryInfo(Application.StartupPath);
            FileInfo[] Files = info.GetFiles("*.welf", SearchOption.AllDirectories);
            foreach (FileInfo file in Files)
            {
                welfBox.Items.Add(file.Name);
            }
        }

        public void StartButtonClick(object sender, EventArgs e)
        {
            ParsingType type = (ParsingType)tabControl1.SelectedIndex;
            string locale = (string)localeBox.SelectedItem;

            _parser = (Parser)Activator.CreateInstance((Type)parserBox.SelectedItem);
            if (_parser == null)
                throw new ArgumentNullException(@"Parser");

            string address = string.Format("http://{0}{1}", (string.IsNullOrEmpty(locale) ? "www." : locale), _parser.Address);

            startButton.Enabled = false;
            abortButton.Enabled = true;
            progressBar.Minimum = 0;
            progressBar.Value = 0;

            switch (type)
            {
                case ParsingType.TypeSingle:
                    {
                        int value = (int)valueBox.Value;
                        if (value < 1)
                            throw new ArgumentOutOfRangeException(@"Value", @"Value can not be smaller than '1'!");

                        _worker = new Worker(value, address, backgroundWorker);
                        break;
                    }
                case ParsingType.TypeList:
                    {
                        if (_entries.Count == -1)
                            throw new NotImplementedException(@"Entries list is empty!");

                        progressBar.Visible = true;
                        progressBar.Value = 1;
                        progressBar.Minimum = 1;
                        progressBar.Maximum = _entries.Count;

                        _worker = new Worker(_entries, address, backgroundWorker);
                        break;
                    }
                case ParsingType.TypeMultiple:
                    {
                        int startValue = (int)rangeStart.Value;
                        int endValue = (int)rangeEnd.Value;

                        if (startValue > endValue)
                            throw new ArgumentOutOfRangeException(@"StartValue", @"Starting value can not be bigger than ending value!");

                        if (startValue == endValue)
                            throw new NotImplementedException(@"Starting value can not be equal ending value!");

                        int dif = endValue - startValue;
                        progressBar.Visible = true;
                        progressBar.Value = 0;
                        progressBar.Minimum = 0;
                        progressBar.Maximum = dif;

                        _worker = new Worker(startValue, endValue, address, backgroundWorker);
                        break;
                    }
                default:
                    throw new NotImplementedException(string.Format(@"Unsupported type: {0}", type));
            }

            progressLabel.Text = "Downloading...";
            if (_worker == null)
                throw new ArgumentNullException(@"Worker");

            _startTime = DateTime.Now;
            _worker.Start();
        }

        public void ParserIndexChanged(object sender, EventArgs e)
        {
            if (parserBox.SelectedItem == null)
            {
                startButton.Enabled = false;
                abortButton.Enabled = false;
                return;
            }

            startButton.Enabled = true;
            abortButton.Enabled = false;
        }

        private void BackgroundWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (progressBar.InvokeRequired)
                progressBar.BeginInvoke(new Action<int>(i => progressBar.Value += i), e.ProgressPercentage);
            else
                progressBar.Value += e.ProgressPercentage;
        }

        void BackgroundWorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_worker == null)
                throw new ArgumentNullException(@"Worker");

            startButton.Enabled = true;
            abortButton.Enabled = false;
            DateTime now = DateTime.Now;

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                progressLabel.Text = "Parsing...";
                using (StreamWriter stream = new StreamWriter(saveDialog.OpenFile(), Encoding.UTF8))
                {
                    stream.WriteLine(@"-- Dump of {0} ({1} - {0}) Total object count: {2}", now, _startTime, _worker.Pages.Count);
                    foreach (Block block in _worker.Pages)
                    {
                        string content = _parser.Parse(block);
                        if (!string.IsNullOrEmpty(content))
                            stream.Write(content);
                    }
                }
            }

            progressLabel.Text = "Complete!";
        }

        private void AbortButtonClick(object sender, EventArgs e)
        {
            if (_worker == null)
                throw new ArgumentNullException(@"Worker");

            backgroundWorker.Dispose();
            _worker.Stop();
            startButton.Enabled = true;
            abortButton.Enabled = false;
            progressLabel.Text = "Abort...";
        }

        private void WelfBoxSelectedIndexChanged(object sender, EventArgs e)
        {
            _entries = new List<uint>();

            if (welfBox.SelectedItem == null)
            {
                startButton.Enabled = false;
                abortButton.Enabled = false;
                return;
            }

            using (StreamReader reader = new StreamReader(Path.Combine("EntryList", (string)welfBox.SelectedItem)))
            {
                string str = reader.ReadToEnd();
                string[] values = str.Split(',');
                foreach (string value in values)
                {
                    uint val;
                    if (uint.TryParse(value, out val))
                    {
                        if (!_entries.Contains(val))
                            _entries.Add(val);
                    }
                }

                entryCountLabel.Text = string.Format("Entry count: {0}", _entries.Count);
            }
        }

        private void ExitToolStripMenuItemClick(object sender, EventArgs e)
        {
            _worker.Stop();
            Application.Exit();
        }

        private void WELFCreatorToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (_creator == null || _creator.IsDisposed)
                _creator = new WelfCreator();
            if (!_creator.Visible)
                _creator.Show(this);
        }

        private void ReloadWelfFilesToolStripMenuItemClick(object sender, EventArgs e)
        {
            welfBox.Items.Clear();

            DirectoryInfo info = new DirectoryInfo(Application.StartupPath);
            FileInfo[] Files = info.GetFiles("*.welf", SearchOption.AllDirectories);
            foreach (FileInfo file in Files)
            {
                welfBox.Items.Add(file.Name);
            }
        }
    }
}
