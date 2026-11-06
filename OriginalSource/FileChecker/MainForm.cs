using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Data.SqlClient;

namespace FileChecker
{
    public partial class MainForm : Form
    {
        private static string _logFileName = Directory.GetCurrentDirectory() + "\\" + "_DMS_Output.txt";
        private static string _iniFileName = Directory.GetCurrentDirectory() + "\\" + "FileChecker.ini";
        private static bool _includeZeroByteFiles = false;

        private static Object _fileLock = new object();
        private static int _queuedItemsCount = 0;
        private static Object _queuedItemsCountLock = new object();
        private static int _missingItemsCount = 0;
        private static Object _missingItemsCountLock = new object();
        private static int _processedCount = 0;
        private static Object _processedItemsCountLock = new object();
        private static bool _bQueuing = false;
        private static bool _bStopRequested = false;
        private static int _maximumQueueSize = 1000000; // Don't queue up more than 1 million items
        
        private static DateTime _dtStartTime = DateTime.Now;
        private static int _totalItemCount = 0;

        public MainForm()
        {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            lblTotalItemCount.Text = "";
            lblRunningTime.Text = "";
            lblItemsPerSecond.Text = "";
            lblRemainingTime.Text = "";
            lblQueueSize.Text = "";
            lblMissingFiles.Text = "";

            txtLogFile.Text = _logFileName;

            LoadIniSettings();
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveIniSettings();
        }
        private void LoadIniSettings()
        {
            try
            {
                if (!File.Exists(_iniFileName))
                    return;

                var lines = File.ReadAllLines(_iniFileName);
                var dict = new Dictionary<string, string>();

                foreach (var s in lines)
                {
                    int iIndex = s.IndexOf("=");
                    if (iIndex >= 0)
                    {
                        string sKey = s.Substring(0, iIndex);
                        string sValue = s.Substring(iIndex + 1);
                        dict.Add(sKey, sValue);
                    }
                }

                if (dict.Keys.Contains("DBSERVER"))
                    txtSQLServer.Text = dict["DBSERVER"];
                if (dict.Keys.Contains("DBUSER"))
                    txtDBUser.Text = dict["DBUSER"];
                if (dict.Keys.Contains("DBPWD"))
                    txtDBPwd.Text = dict["DBPWD"];
                if (dict.Keys.Contains("DBNAME"))
                    txtDBName.Text = dict["DBNAME"];
                if (dict.Keys.Contains("DBQUERY"))
                    txtSQLQuery.Text = dict["DBQUERY"];
                if (dict.Keys.Contains("LOGFILE"))
                    txtLogFile.Text = dict["LOGFILE"];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void SaveIniSettings()
        {
            try
            {
                string sData = $"DBSERVER={txtSQLServer.Text}\n";
                sData += $"DBUSER={txtDBUser.Text}\n";
                sData += $"DBPWD={txtDBPwd.Text}\n";
                sData += $"DBNAME={txtDBName.Text}\n";
                sData += $"DBQUERY={txtSQLQuery.Text}\n";
                sData += $"LOGFILE={txtLogFile.Text}\n";
                File.WriteAllText(_iniFileName, sData + "\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            lblStatus.Text = "Starting...";
            lblTotalItemCount.Text = "";
            lblQueueSize.Text = "";
            lblMissingFiles.Text = "";
            lblRunningTime.Text = "";
            lblRemainingTime.Text = "";
            lblItemsPerSecond.Text = "";
            lblProcessedCnt.Text = "";
            pgPercent.Value = 0;
            _logFileName = txtLogFile.Text;
            _includeZeroByteFiles = chkZeroByteFiles.Checked;

            _processedCount = 0;
            _queuedItemsCount = 0;
            _missingItemsCount = 0;
            _bStopRequested = false;
            _bQueuing = true;
            timer1.Start();
            timer2.Start();

            bgWorker.RunWorkerAsync();
        }

        private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                LogData("MissingID\tMissingFile", true);

                using (SqlConnection conn = new SqlConnection())
                {
                    _totalItemCount = 0;
                    _dtStartTime = DateTime.Now;
                    if (txtDBUser.Text == "" && txtDBPwd.Text == "")
                        conn.ConnectionString = "Data Source=" + txtSQLServer.Text + "; Initial Catalog=" + txtDBName.Text + "; Integrated Security=True";
                    else
                        conn.ConnectionString = "Data Source=" + txtSQLServer.Text + "; Initial Catalog=" + txtDBName.Text + "; Integrated Security=False; User ID=" + txtDBUser.Text + "; Password=" + txtDBPwd.Text;

                    conn.Open();

                    // First determine our row counts
                    string sRowCountSQL = "SELECT COUNT(1) FROM (" + txtSQLQuery.Text + ") AS RETVAL";
                    SqlCommand command = new SqlCommand(sRowCountSQL, conn);
                    command.CommandTimeout = 1000;
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        _totalItemCount = reader.GetInt32(0);
                    }
                    reader.Close();

                    // Now go get the data
                    command = new SqlCommand(txtSQLQuery.Text, conn);
                    command.CommandTimeout = 1000;
                    reader = command.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            while (_queuedItemsCount >= _maximumQueueSize && !_bStopRequested)
                            {
                                Thread.Sleep(1000);
                            }

                            if (_bStopRequested)
                            {
                                command.Cancel();
                                break;
                            }

                            AddQueuedCount();
                            MQThreadData oParam = new MQThreadData(reader[0].ToString(), reader[1].ToString());
                            ThreadPool.QueueUserWorkItem(new WaitCallback(DoWork), oParam);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                    finally
                    {
                        _bQueuing = false;
                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                _bQueuing = false;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _bStopRequested = true;
        }
        private void btnViewLog_Click(object sender, EventArgs e)
        {
            if (File.Exists(txtLogFile.Text))
                System.Diagnostics.Process.Start(txtLogFile.Text);
            else
                MessageBox.Show("File Not Found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static void DoWork(Object oParam)
        {
            try
            {
                if (_bStopRequested)
                    return;

                MQThreadData mQThreadData = (MQThreadData)oParam;
                if (File.Exists(mQThreadData.DocumentPath) == false)
                {
                    LogData(mQThreadData.DocumentKey + "\t" + mQThreadData.DocumentPath);
                    AddMissingCount();
                }
                else if (_includeZeroByteFiles)
                {
                    long length = new FileInfo(mQThreadData.DocumentPath).Length;
                    if (length <= 0)
                    {
                        LogData(mQThreadData.DocumentKey + "\t" + mQThreadData.DocumentPath);
                        AddMissingCount();
                    }
                }
            }
            finally
            {
                SubtractQueuedCount();
            }
        }

        private static void AddQueuedCount()
        {
            lock (_queuedItemsCountLock)
            {
                _queuedItemsCount++;
            }
        }

        private static void SubtractQueuedCount()
        {
            lock (_queuedItemsCountLock)
            {
                _queuedItemsCount--;
            }

            if (!_bStopRequested)
                AddProcessedCount();
        }

        private static void AddMissingCount()
        {
            lock (_missingItemsCountLock)
            {
                _missingItemsCount++;
            }
        }

        private static void AddProcessedCount()
        {
            lock (_processedItemsCountLock)
            {
                _processedCount++;
            }
        }

        private static void LogData(string sData, bool bCreateFile = false)
        {
            lock (_fileLock)
            {
                if (bCreateFile)
                {
                    File.WriteAllText(_logFileName, sData + "\n");
                }
                else
                {
                    File.AppendAllText(_logFileName, sData + "\n");
                }
            }
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_queuedItemsCount == 0 && _bQueuing == false)
            {
                timer1.Stop();
                timer2.Stop();
                btnStart.Enabled = true;
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_totalItemCount == 0)
            {
                lblStatus.Text = "Computing Count Size";
                return;
            }

            if (_queuedItemsCount == 0)
            {
                UpdateTimeEstimates();
                lblRemainingTime.Text = "";

                if (_bStopRequested)
                {
                    lblStatus.Text = "Canceled";
                    pgPercent.Value = 0;
                }
                else
                {
                    lblStatus.Text = "Complete";
                    pgPercent.Value = 100;
                }
            }
            else
            {
                if (_bQueuing == true)
                {
                    lblStatus.Text = "Queuing and Processing";
                }
                else
                {
                    lblStatus.Text = "Processing";
                }
            }

            lblTotalItemCount.Text = _totalItemCount.ToString("N0");
            lblProcessedCnt.Text = _processedCount.ToString("N0");
            lblQueueSize.Text = _queuedItemsCount.ToString("N0");
            lblMissingFiles.Text = _missingItemsCount.ToString("N0");
            btnStart.Enabled = _queuedItemsCount == 0;
            btnStop.Enabled = _queuedItemsCount != 0;
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            UpdateTimeEstimates();
        }
        private void UpdateTimeEstimates()
        {
            try
            {
                if (_totalItemCount == 0)
                {
                    return;
                }

                TimeSpan tsSpent = DateTime.Now - _dtStartTime;
                double dItemsPerSecond = _processedCount / tsSpent.TotalSeconds;
                double dPctComplete = (double)_processedCount / (double)_totalItemCount;
                TimeSpan tsTimeOverall = TimeSpan.FromTicks((long)(tsSpent.Ticks / dPctComplete));
                TimeSpan dtTimeRemaining = tsTimeOverall - tsSpent;

                lblRunningTime.Text = string.Format("{0:00}:{1:00}:{2:00}", tsSpent.Hours, tsSpent.Minutes, tsSpent.Seconds);
                lblItemsPerSecond.Text = string.Format("{0:0.00}", dItemsPerSecond);
                lblRemainingTime.Text = string.Format("{0:00}:{1:00}:{2:00}", dtTimeRemaining.Hours, dtTimeRemaining.Minutes, dtTimeRemaining.Seconds);

                if (!_bStopRequested)
                    pgPercent.Value = Convert.ToInt32(dPctComplete * 100);
            }
            catch { }
        }
    }

    class MQThreadData
    {
        public string DocumentKey;
        public string DocumentPath;

        public MQThreadData(string docKey, string docPath)
        {
            DocumentKey = docKey;
            DocumentPath = docPath;
        }
    }
}
