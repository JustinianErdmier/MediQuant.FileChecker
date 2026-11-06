namespace FileChecker
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnStart = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.txtSQLServer = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtSQLQuery = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lblQueueSize = new System.Windows.Forms.Label();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.label6 = new System.Windows.Forms.Label();
            this.txtDBName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.lblMissingFiles = new System.Windows.Forms.Label();
            this.btnStop = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.lblRunningTime = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.lblRemainingTime = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.lblItemsPerSecond = new System.Windows.Forms.Label();
            this.timer2 = new System.Windows.Forms.Timer(this.components);
            this.label8 = new System.Windows.Forms.Label();
            this.lblTotalItemCount = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.txtLogFile = new System.Windows.Forms.TextBox();
            this.btnViewLog = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.pgPercent = new System.Windows.Forms.ProgressBar();
            this.label15 = new System.Windows.Forms.Label();
            this.lblProcessedCnt = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.bgWorker = new System.ComponentModel.BackgroundWorker();
            this.label14 = new System.Windows.Forms.Label();
            this.txtDBUser = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.txtDBPwd = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.chkZeroByteFiles = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(713, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 7;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(68, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "SQL Server :";
            // 
            // txtSQLServer
            // 
            this.txtSQLServer.Location = new System.Drawing.Point(90, 9);
            this.txtSQLServer.Name = "txtSQLServer";
            this.txtSQLServer.Size = new System.Drawing.Size(258, 20);
            this.txtSQLServer.TabIndex = 0;
            this.txtSQLServer.Text = "KDCSQL15";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 117);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Select Query :";
            // 
            // txtSQLQuery
            // 
            this.txtSQLQuery.Location = new System.Drawing.Point(90, 114);
            this.txtSQLQuery.Name = "txtSQLQuery";
            this.txtSQLQuery.Size = new System.Drawing.Size(617, 20);
            this.txtSQLQuery.TabIndex = 4;
            this.txtSQLQuery.Text = "SELECT TOP 100 doc_id, MQ_FullPath FROM _DMS_Generic_Index";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(23, 28);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(43, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Status :";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(110, 28);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(24, 13);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "Idle";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(23, 85);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(68, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "Queue Size :";
            // 
            // lblQueueSize
            // 
            this.lblQueueSize.AutoSize = true;
            this.lblQueueSize.Location = new System.Drawing.Point(110, 86);
            this.lblQueueSize.Name = "lblQueueSize";
            this.lblQueueSize.Size = new System.Drawing.Size(13, 13);
            this.lblQueueSize.TabIndex = 3;
            this.lblQueueSize.Text = "0";
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(12, 91);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(59, 13);
            this.label6.TabIndex = 1;
            this.label6.Text = "DB Name :";
            // 
            // txtDBName
            // 
            this.txtDBName.Location = new System.Drawing.Point(90, 88);
            this.txtDBName.Name = "txtDBName";
            this.txtDBName.Size = new System.Drawing.Size(617, 20);
            this.txtDBName.TabIndex = 3;
            this.txtDBName.Text = "InterimBJC_Soarian_DocStorage_12729";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(23, 105);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(72, 13);
            this.label4.TabIndex = 3;
            this.label4.Text = "Missing Files :";
            // 
            // lblMissingFiles
            // 
            this.lblMissingFiles.AutoSize = true;
            this.lblMissingFiles.Location = new System.Drawing.Point(110, 106);
            this.lblMissingFiles.Name = "lblMissingFiles";
            this.lblMissingFiles.Size = new System.Drawing.Size(13, 13);
            this.lblMissingFiles.TabIndex = 3;
            this.lblMissingFiles.Text = "0";
            // 
            // btnStop
            // 
            this.btnStop.Enabled = false;
            this.btnStop.Location = new System.Drawing.Point(713, 41);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 23);
            this.btnStop.TabIndex = 8;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(23, 125);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(59, 13);
            this.label7.TabIndex = 3;
            this.label7.Text = "Run Time :";
            // 
            // lblRunningTime
            // 
            this.lblRunningTime.AutoSize = true;
            this.lblRunningTime.Location = new System.Drawing.Point(110, 126);
            this.lblRunningTime.Name = "lblRunningTime";
            this.lblRunningTime.Size = new System.Drawing.Size(13, 13);
            this.lblRunningTime.TabIndex = 3;
            this.lblRunningTime.Text = "0";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(23, 145);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(75, 13);
            this.label9.TabIndex = 3;
            this.label9.Text = "Remain Time :";
            // 
            // lblRemainingTime
            // 
            this.lblRemainingTime.AutoSize = true;
            this.lblRemainingTime.Location = new System.Drawing.Point(110, 146);
            this.lblRemainingTime.Name = "lblRemainingTime";
            this.lblRemainingTime.Size = new System.Drawing.Size(13, 13);
            this.lblRemainingTime.TabIndex = 3;
            this.lblRemainingTime.Text = "0";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(23, 165);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(86, 13);
            this.label11.TabIndex = 3;
            this.label11.Text = "Items / Second :";
            // 
            // lblItemsPerSecond
            // 
            this.lblItemsPerSecond.AutoSize = true;
            this.lblItemsPerSecond.Location = new System.Drawing.Point(110, 166);
            this.lblItemsPerSecond.Name = "lblItemsPerSecond";
            this.lblItemsPerSecond.Size = new System.Drawing.Size(13, 13);
            this.lblItemsPerSecond.TabIndex = 3;
            this.lblItemsPerSecond.Text = "0";
            // 
            // timer2
            // 
            this.timer2.Interval = 1000;
            this.timer2.Tick += new System.EventHandler(this.timer2_Tick);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(23, 48);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(65, 13);
            this.label8.TabIndex = 3;
            this.label8.Text = "Total Items :";
            // 
            // lblTotalItemCount
            // 
            this.lblTotalItemCount.AutoSize = true;
            this.lblTotalItemCount.Location = new System.Drawing.Point(110, 48);
            this.lblTotalItemCount.Name = "lblTotalItemCount";
            this.lblTotalItemCount.Size = new System.Drawing.Size(13, 13);
            this.lblTotalItemCount.TabIndex = 3;
            this.lblTotalItemCount.Text = "0";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(12, 143);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(50, 13);
            this.label10.TabIndex = 1;
            this.label10.Text = "Log File :";
            // 
            // txtLogFile
            // 
            this.txtLogFile.Location = new System.Drawing.Point(90, 140);
            this.txtLogFile.Name = "txtLogFile";
            this.txtLogFile.Size = new System.Drawing.Size(617, 20);
            this.txtLogFile.TabIndex = 5;
            // 
            // btnViewLog
            // 
            this.btnViewLog.Location = new System.Drawing.Point(713, 138);
            this.btnViewLog.Name = "btnViewLog";
            this.btnViewLog.Size = new System.Drawing.Size(75, 23);
            this.btnViewLog.TabIndex = 9;
            this.btnViewLog.Text = "View";
            this.btnViewLog.UseVisualStyleBackColor = true;
            this.btnViewLog.Click += new System.EventHandler(this.btnViewLog_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.pgPercent);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.lblStatus);
            this.groupBox1.Controls.Add(this.lblItemsPerSecond);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.lblRemainingTime);
            this.groupBox1.Controls.Add(this.label15);
            this.groupBox1.Controls.Add(this.label8);
            this.groupBox1.Controls.Add(this.lblRunningTime);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.lblMissingFiles);
            this.groupBox1.Controls.Add(this.lblProcessedCnt);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.lblTotalItemCount);
            this.groupBox1.Controls.Add(this.label12);
            this.groupBox1.Controls.Add(this.label11);
            this.groupBox1.Controls.Add(this.lblQueueSize);
            this.groupBox1.Location = new System.Drawing.Point(15, 203);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(692, 231);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Status Information";
            // 
            // pgPercent
            // 
            this.pgPercent.Location = new System.Drawing.Point(113, 183);
            this.pgPercent.Name = "pgPercent";
            this.pgPercent.Size = new System.Drawing.Size(573, 23);
            this.pgPercent.TabIndex = 4;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(23, 66);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(82, 13);
            this.label15.TabIndex = 3;
            this.label15.Text = "Processed Cnt :";
            // 
            // lblProcessedCnt
            // 
            this.lblProcessedCnt.AutoSize = true;
            this.lblProcessedCnt.Location = new System.Drawing.Point(110, 66);
            this.lblProcessedCnt.Name = "lblProcessedCnt";
            this.lblProcessedCnt.Size = new System.Drawing.Size(13, 13);
            this.lblProcessedCnt.TabIndex = 3;
            this.lblProcessedCnt.Text = "0";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(23, 186);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(76, 13);
            this.label12.TabIndex = 3;
            this.label12.Text = "Pct Complete :";
            // 
            // bgWorker
            // 
            this.bgWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.bgWorker_DoWork);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(12, 38);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(59, 13);
            this.label14.TabIndex = 1;
            this.label14.Text = "SQL User :";
            // 
            // txtDBUser
            // 
            this.txtDBUser.Location = new System.Drawing.Point(90, 35);
            this.txtDBUser.Name = "txtDBUser";
            this.txtDBUser.Size = new System.Drawing.Size(258, 20);
            this.txtDBUser.TabIndex = 1;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(12, 64);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(58, 13);
            this.label16.TabIndex = 1;
            this.label16.Text = "SQL Pwd :";
            // 
            // txtDBPwd
            // 
            this.txtDBPwd.Location = new System.Drawing.Point(90, 61);
            this.txtDBPwd.Name = "txtDBPwd";
            this.txtDBPwd.Size = new System.Drawing.Size(258, 20);
            this.txtDBPwd.TabIndex = 2;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(355, 38);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(225, 13);
            this.label17.TabIndex = 8;
            this.label17.Text = "Leave User/Pwd Blank for Integrated Security";
            // 
            // chkZeroByteFiles
            // 
            this.chkZeroByteFiles.AutoSize = true;
            this.chkZeroByteFiles.Checked = true;
            this.chkZeroByteFiles.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkZeroByteFiles.Location = new System.Drawing.Point(15, 175);
            this.chkZeroByteFiles.Name = "chkZeroByteFiles";
            this.chkZeroByteFiles.Size = new System.Drawing.Size(186, 17);
            this.chkZeroByteFiles.TabIndex = 10;
            this.chkZeroByteFiles.Text = "Include Zero Byte Files as Missing";
            this.chkZeroByteFiles.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 454);
            this.Controls.Add(this.chkZeroByteFiles);
            this.Controls.Add(this.label17);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnViewLog);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.txtLogFile);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.txtSQLQuery);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtDBName);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.txtDBPwd);
            this.Controls.Add(this.label16);
            this.Controls.Add(this.txtDBUser);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.txtSQLServer);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "File Checker";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtSQLServer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtSQLQuery;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label lblQueueSize;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtDBName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lblMissingFiles;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label lblRunningTime;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label lblRemainingTime;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label lblItemsPerSecond;
        private System.Windows.Forms.Timer timer2;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label lblTotalItemCount;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox txtLogFile;
        private System.Windows.Forms.Button btnViewLog;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.ProgressBar pgPercent;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label lblProcessedCnt;
        private System.ComponentModel.BackgroundWorker bgWorker;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox txtDBUser;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox txtDBPwd;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.CheckBox chkZeroByteFiles;
    }
}

