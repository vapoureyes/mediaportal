namespace SetupTv.Sections
{
  partial class CardDvbT
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
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageScan = new System.Windows.Forms.TabPage();
            this.checkBoxEnableChannelMoveDetection = new System.Windows.Forms.CheckBox();
            this.checkBoxCreateSignalGroup = new System.Windows.Forms.CheckBox();
            this.checkBoxAdvancedTuning = new System.Windows.Forms.CheckBox();
            this.mpGrpAdvancedTuning = new MediaPortal.UserInterface.Controls.MPGroupBox();
            this.mpLabel7 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.mpLabel2 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.scanNIT = new System.Windows.Forms.RadioButton();
            this.scanSingleTransponder = new System.Windows.Forms.RadioButton();
            this.scanPredefProvider = new System.Windows.Forms.RadioButton();
            this.mpLabel5 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.textBoxFreq = new System.Windows.Forms.TextBox();
            this.textBoxBandwidth = new System.Windows.Forms.TextBox();
            this.mpLabel4 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.mpGrpScanProgress = new MediaPortal.UserInterface.Controls.MPGroupBox();
            this.mpLabel3 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.progressBarLevel = new System.Windows.Forms.ProgressBar();
            this.progressBarQuality = new System.Windows.Forms.ProgressBar();
            this.listViewStatus = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.checkBoxCreateGroups = new System.Windows.Forms.CheckBox();
            this.mpButtonScanTv = new MediaPortal.UserInterface.Controls.MPButton();
            this.mpLabel6 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.mpLabel1 = new MediaPortal.UserInterface.Controls.MPLabel();
            this.mpComboBoxRegion = new MediaPortal.UserInterface.Controls.MPComboBox();
            this.mpComboBoxCountry = new MediaPortal.UserInterface.Controls.MPComboBox();
            this.tabPageCIMenu = new System.Windows.Forms.TabPage();
            this.checkBoxEnableStrongestChannelSelection = new System.Windows.Forms.CheckBox();
            this.tabControl1.SuspendLayout();
            this.tabPageScan.SuspendLayout();
            this.mpGrpAdvancedTuning.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.mpGrpScanProgress.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPageScan);
            this.tabControl1.Controls.Add(this.tabPageCIMenu);
            this.tabControl1.Location = new System.Drawing.Point(5, 5);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(744, 533);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPageScan
            // 
            this.tabPageScan.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageScan.Controls.Add(this.checkBoxEnableStrongestChannelSelection);
            this.tabPageScan.Controls.Add(this.checkBoxEnableChannelMoveDetection);
            this.tabPageScan.Controls.Add(this.checkBoxCreateSignalGroup);
            this.tabPageScan.Controls.Add(this.checkBoxAdvancedTuning);
            this.tabPageScan.Controls.Add(this.mpGrpAdvancedTuning);
            this.tabPageScan.Controls.Add(this.mpGrpScanProgress);
            this.tabPageScan.Controls.Add(this.checkBoxCreateGroups);
            this.tabPageScan.Controls.Add(this.mpButtonScanTv);
            this.tabPageScan.Controls.Add(this.mpLabel6);
            this.tabPageScan.Controls.Add(this.mpLabel1);
            this.tabPageScan.Controls.Add(this.mpComboBoxRegion);
            this.tabPageScan.Controls.Add(this.mpComboBoxCountry);
            this.tabPageScan.Location = new System.Drawing.Point(4, 25);
            this.tabPageScan.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPageScan.Name = "tabPageScan";
            this.tabPageScan.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPageScan.Size = new System.Drawing.Size(736, 504);
            this.tabPageScan.TabIndex = 0;
            this.tabPageScan.Text = "Scanning";
            // 
            // checkBoxEnableChannelMoveDetection
            // 
            this.checkBoxEnableChannelMoveDetection.AutoSize = true;
            this.checkBoxEnableChannelMoveDetection.Location = new System.Drawing.Point(17, 105);
            this.checkBoxEnableChannelMoveDetection.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkBoxEnableChannelMoveDetection.Name = "checkBoxEnableChannelMoveDetection";
            this.checkBoxEnableChannelMoveDetection.Size = new System.Drawing.Size(259, 21);
            this.checkBoxEnableChannelMoveDetection.TabIndex = 6;
            this.checkBoxEnableChannelMoveDetection.Text = "Enable channel movement detection";
            this.checkBoxEnableChannelMoveDetection.UseVisualStyleBackColor = true;
            // 
            // checkBoxCreateSignalGroup
            // 
            this.checkBoxCreateSignalGroup.AutoSize = true;
            this.checkBoxCreateSignalGroup.Location = new System.Drawing.Point(293, 76);
            this.checkBoxCreateSignalGroup.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkBoxCreateSignalGroup.Name = "checkBoxCreateSignalGroup";
            this.checkBoxCreateSignalGroup.Size = new System.Drawing.Size(235, 21);
            this.checkBoxCreateSignalGroup.TabIndex = 5;
            this.checkBoxCreateSignalGroup.Text = "Create \"Digital Terrestrial\" group";
            this.checkBoxCreateSignalGroup.UseVisualStyleBackColor = true;
            this.checkBoxCreateSignalGroup.CheckedChanged += new System.EventHandler(this.checkBoxCreateSignalGroup_CheckedChanged);
            // 
            // checkBoxAdvancedTuning
            // 
            this.checkBoxAdvancedTuning.AutoSize = true;
            this.checkBoxAdvancedTuning.Location = new System.Drawing.Point(17, 133);
            this.checkBoxAdvancedTuning.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkBoxAdvancedTuning.Name = "checkBoxAdvancedTuning";
            this.checkBoxAdvancedTuning.Size = new System.Drawing.Size(214, 21);
            this.checkBoxAdvancedTuning.TabIndex = 7;
            this.checkBoxAdvancedTuning.Text = "Use advanced tuning options";
            this.checkBoxAdvancedTuning.UseVisualStyleBackColor = true;
            this.checkBoxAdvancedTuning.CheckedChanged += new System.EventHandler(this.UpdateGUIControls);
            // 
            // mpGrpAdvancedTuning
            // 
            this.mpGrpAdvancedTuning.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mpGrpAdvancedTuning.Controls.Add(this.mpLabel7);
            this.mpGrpAdvancedTuning.Controls.Add(this.mpLabel2);
            this.mpGrpAdvancedTuning.Controls.Add(this.groupBox2);
            this.mpGrpAdvancedTuning.Controls.Add(this.mpLabel5);
            this.mpGrpAdvancedTuning.Controls.Add(this.textBoxFreq);
            this.mpGrpAdvancedTuning.Controls.Add(this.textBoxBandwidth);
            this.mpGrpAdvancedTuning.Controls.Add(this.mpLabel4);
            this.mpGrpAdvancedTuning.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.mpGrpAdvancedTuning.Location = new System.Drawing.Point(3, 316);
            this.mpGrpAdvancedTuning.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpGrpAdvancedTuning.Name = "mpGrpAdvancedTuning";
            this.mpGrpAdvancedTuning.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpGrpAdvancedTuning.Size = new System.Drawing.Size(723, 151);
            this.mpGrpAdvancedTuning.TabIndex = 8;
            this.mpGrpAdvancedTuning.TabStop = false;
            this.mpGrpAdvancedTuning.Text = "Advanced tuning options";
            this.mpGrpAdvancedTuning.Visible = false;
            // 
            // mpLabel7
            // 
            this.mpLabel7.AutoSize = true;
            this.mpLabel7.Location = new System.Drawing.Point(205, 66);
            this.mpLabel7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel7.Name = "mpLabel7";
            this.mpLabel7.Size = new System.Drawing.Size(36, 17);
            this.mpLabel7.TabIndex = 14;
            this.mpLabel7.Text = "MHz";
            // 
            // mpLabel2
            // 
            this.mpLabel2.AutoSize = true;
            this.mpLabel2.Location = new System.Drawing.Point(11, 38);
            this.mpLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel2.Name = "mpLabel2";
            this.mpLabel2.Size = new System.Drawing.Size(79, 17);
            this.mpLabel2.TabIndex = 9;
            this.mpLabel2.Text = "Frequency:";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.scanNIT);
            this.groupBox2.Controls.Add(this.scanSingleTransponder);
            this.groupBox2.Controls.Add(this.scanPredefProvider);
            this.groupBox2.Location = new System.Drawing.Point(356, 20);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox2.Size = new System.Drawing.Size(232, 110);
            this.groupBox2.TabIndex = 15;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Scan type";
            // 
            // scanNIT
            // 
            this.scanNIT.AutoSize = true;
            this.scanNIT.Location = new System.Drawing.Point(9, 76);
            this.scanNIT.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.scanNIT.Name = "scanNIT";
            this.scanNIT.Size = new System.Drawing.Size(195, 21);
            this.scanNIT.TabIndex = 18;
            this.scanNIT.Text = "search for transponder list";
            this.scanNIT.UseVisualStyleBackColor = true;
            this.scanNIT.CheckedChanged += new System.EventHandler(this.UpdateGUIControls);
            // 
            // scanSingleTransponder
            // 
            this.scanSingleTransponder.AutoSize = true;
            this.scanSingleTransponder.Location = new System.Drawing.Point(9, 48);
            this.scanSingleTransponder.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.scanSingleTransponder.Name = "scanSingleTransponder";
            this.scanSingleTransponder.Size = new System.Drawing.Size(147, 21);
            this.scanSingleTransponder.TabIndex = 17;
            this.scanSingleTransponder.Text = "single transponder";
            this.scanSingleTransponder.UseVisualStyleBackColor = true;
            this.scanSingleTransponder.CheckedChanged += new System.EventHandler(this.UpdateGUIControls);
            // 
            // scanPredefProvider
            // 
            this.scanPredefProvider.AutoSize = true;
            this.scanPredefProvider.Checked = true;
            this.scanPredefProvider.Location = new System.Drawing.Point(9, 20);
            this.scanPredefProvider.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.scanPredefProvider.Name = "scanPredefProvider";
            this.scanPredefProvider.Size = new System.Drawing.Size(153, 21);
            this.scanPredefProvider.TabIndex = 16;
            this.scanPredefProvider.TabStop = true;
            this.scanPredefProvider.Text = "predefined provider";
            this.scanPredefProvider.UseVisualStyleBackColor = true;
            this.scanPredefProvider.CheckedChanged += new System.EventHandler(this.UpdateGUIControls);
            // 
            // mpLabel5
            // 
            this.mpLabel5.AutoSize = true;
            this.mpLabel5.Location = new System.Drawing.Point(205, 38);
            this.mpLabel5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel5.Name = "mpLabel5";
            this.mpLabel5.Size = new System.Drawing.Size(32, 17);
            this.mpLabel5.TabIndex = 11;
            this.mpLabel5.Text = "kHz";
            // 
            // textBoxFreq
            // 
            this.textBoxFreq.Location = new System.Drawing.Point(131, 34);
            this.textBoxFreq.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxFreq.MaxLength = 6;
            this.textBoxFreq.Name = "textBoxFreq";
            this.textBoxFreq.Size = new System.Drawing.Size(65, 22);
            this.textBoxFreq.TabIndex = 10;
            this.textBoxFreq.Text = "163000";
            // 
            // textBoxBandwidth
            // 
            this.textBoxBandwidth.Location = new System.Drawing.Point(131, 63);
            this.textBoxBandwidth.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxBandwidth.MaxLength = 2;
            this.textBoxBandwidth.Name = "textBoxBandwidth";
            this.textBoxBandwidth.Size = new System.Drawing.Size(65, 22);
            this.textBoxBandwidth.TabIndex = 13;
            this.textBoxBandwidth.Text = "8";
            // 
            // mpLabel4
            // 
            this.mpLabel4.AutoSize = true;
            this.mpLabel4.Location = new System.Drawing.Point(11, 66);
            this.mpLabel4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel4.Name = "mpLabel4";
            this.mpLabel4.Size = new System.Drawing.Size(77, 17);
            this.mpLabel4.TabIndex = 12;
            this.mpLabel4.Text = "Bandwidth:";
            // 
            // mpGrpScanProgress
            // 
            this.mpGrpScanProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.mpGrpScanProgress.Controls.Add(this.mpLabel3);
            this.mpGrpScanProgress.Controls.Add(this.progressBar1);
            this.mpGrpScanProgress.Controls.Add(this.label1);
            this.mpGrpScanProgress.Controls.Add(this.label2);
            this.mpGrpScanProgress.Controls.Add(this.progressBarLevel);
            this.mpGrpScanProgress.Controls.Add(this.progressBarQuality);
            this.mpGrpScanProgress.Controls.Add(this.listViewStatus);
            this.mpGrpScanProgress.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.mpGrpScanProgress.Location = new System.Drawing.Point(3, 161);
            this.mpGrpScanProgress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpGrpScanProgress.Name = "mpGrpScanProgress";
            this.mpGrpScanProgress.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpGrpScanProgress.Size = new System.Drawing.Size(727, 332);
            this.mpGrpScanProgress.TabIndex = 19;
            this.mpGrpScanProgress.TabStop = false;
            this.mpGrpScanProgress.Text = "Scan progress";
            this.mpGrpScanProgress.Visible = false;
            // 
            // mpLabel3
            // 
            this.mpLabel3.AutoSize = true;
            this.mpLabel3.Location = new System.Drawing.Point(11, 27);
            this.mpLabel3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel3.Name = "mpLabel3";
            this.mpLabel3.Size = new System.Drawing.Size(113, 17);
            this.mpLabel3.TabIndex = 20;
            this.mpLabel3.Text = "Current channel:";
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(16, 114);
            this.progressBar1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(707, 12);
            this.progressBar1.TabIndex = 25;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 53);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 17);
            this.label1.TabIndex = 21;
            this.label1.Text = "Signal level:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(11, 81);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 17);
            this.label2.TabIndex = 23;
            this.label2.Text = "Signal quality:";
            // 
            // progressBarLevel
            // 
            this.progressBarLevel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarLevel.Location = new System.Drawing.Point(131, 57);
            this.progressBarLevel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.progressBarLevel.Name = "progressBarLevel";
            this.progressBarLevel.Size = new System.Drawing.Size(592, 12);
            this.progressBarLevel.TabIndex = 22;
            // 
            // progressBarQuality
            // 
            this.progressBarQuality.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarQuality.Location = new System.Drawing.Point(131, 85);
            this.progressBarQuality.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.progressBarQuality.Name = "progressBarQuality";
            this.progressBarQuality.Size = new System.Drawing.Size(592, 12);
            this.progressBarQuality.TabIndex = 24;
            // 
            // listViewStatus
            // 
            this.listViewStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewStatus.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1});
            this.listViewStatus.HideSelection = false;
            this.listViewStatus.Location = new System.Drawing.Point(1, 134);
            this.listViewStatus.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.listViewStatus.Name = "listViewStatus";
            this.listViewStatus.Size = new System.Drawing.Size(720, 190);
            this.listViewStatus.TabIndex = 26;
            this.listViewStatus.UseCompatibleStateImageBehavior = false;
            this.listViewStatus.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Status";
            this.columnHeader1.Width = 450;
            // 
            // checkBoxCreateGroups
            // 
            this.checkBoxCreateGroups.AutoSize = true;
            this.checkBoxCreateGroups.Location = new System.Drawing.Point(17, 76);
            this.checkBoxCreateGroups.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkBoxCreateGroups.Name = "checkBoxCreateGroups";
            this.checkBoxCreateGroups.Size = new System.Drawing.Size(232, 21);
            this.checkBoxCreateGroups.TabIndex = 4;
            this.checkBoxCreateGroups.Text = "Create groups for each provider";
            this.checkBoxCreateGroups.UseVisualStyleBackColor = true;
            // 
            // mpButtonScanTv
            // 
            this.mpButtonScanTv.Location = new System.Drawing.Point(448, 34);
            this.mpButtonScanTv.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpButtonScanTv.Name = "mpButtonScanTv";
            this.mpButtonScanTv.Size = new System.Drawing.Size(143, 28);
            this.mpButtonScanTv.TabIndex = 27;
            this.mpButtonScanTv.Text = "Scan for channels";
            this.mpButtonScanTv.UseVisualStyleBackColor = true;
            this.mpButtonScanTv.Click += new System.EventHandler(this.mpButtonScanTv_Click_1);
            // 
            // mpLabel6
            // 
            this.mpLabel6.AutoSize = true;
            this.mpLabel6.Location = new System.Drawing.Point(13, 41);
            this.mpLabel6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel6.Name = "mpLabel6";
            this.mpLabel6.Size = new System.Drawing.Size(114, 17);
            this.mpLabel6.TabIndex = 2;
            this.mpLabel6.Text = "Region/Provider:";
            // 
            // mpLabel1
            // 
            this.mpLabel1.AutoSize = true;
            this.mpLabel1.Location = new System.Drawing.Point(13, 7);
            this.mpLabel1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.mpLabel1.Name = "mpLabel1";
            this.mpLabel1.Size = new System.Drawing.Size(61, 17);
            this.mpLabel1.TabIndex = 0;
            this.mpLabel1.Text = "Country:";
            // 
            // mpComboBoxRegion
            // 
            this.mpComboBoxRegion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.mpComboBoxRegion.FormattingEnabled = true;
            this.mpComboBoxRegion.Location = new System.Drawing.Point(133, 37);
            this.mpComboBoxRegion.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpComboBoxRegion.Name = "mpComboBoxRegion";
            this.mpComboBoxRegion.Size = new System.Drawing.Size(297, 24);
            this.mpComboBoxRegion.TabIndex = 3;
            // 
            // mpComboBoxCountry
            // 
            this.mpComboBoxCountry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.mpComboBoxCountry.FormattingEnabled = true;
            this.mpComboBoxCountry.Location = new System.Drawing.Point(133, 4);
            this.mpComboBoxCountry.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.mpComboBoxCountry.Name = "mpComboBoxCountry";
            this.mpComboBoxCountry.Size = new System.Drawing.Size(297, 24);
            this.mpComboBoxCountry.TabIndex = 1;
            // 
            // tabPageCIMenu
            // 
            this.tabPageCIMenu.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageCIMenu.Location = new System.Drawing.Point(4, 25);
            this.tabPageCIMenu.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPageCIMenu.Name = "tabPageCIMenu";
            this.tabPageCIMenu.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tabPageCIMenu.Size = new System.Drawing.Size(736, 504);
            this.tabPageCIMenu.TabIndex = 1;
            this.tabPageCIMenu.Text = "CI Menu";
            // 
            // checkBoxEnableStrongestChannelSelection
            // 
            this.checkBoxEnableStrongestChannelSelection.AutoSize = true;
            this.checkBoxEnableStrongestChannelSelection.Location = new System.Drawing.Point(293, 105);
            this.checkBoxEnableStrongestChannelSelection.Margin = new System.Windows.Forms.Padding(4);
            this.checkBoxEnableStrongestChannelSelection.Name = "checkBoxEnableStrongestChannelSelection";
            this.checkBoxEnableStrongestChannelSelection.Size = new System.Drawing.Size(255, 21);
            this.checkBoxEnableStrongestChannelSelection.TabIndex = 28;
            this.checkBoxEnableStrongestChannelSelection.Text = "Enable Strongest Channel selection";
            this.checkBoxEnableStrongestChannelSelection.UseVisualStyleBackColor = true;
            // 
            // CardDvbT
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabControl1);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "CardDvbT";
            this.Size = new System.Drawing.Size(753, 543);
            this.tabControl1.ResumeLayout(false);
            this.tabPageScan.ResumeLayout(false);
            this.tabPageScan.PerformLayout();
            this.mpGrpAdvancedTuning.ResumeLayout(false);
            this.mpGrpAdvancedTuning.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.mpGrpScanProgress.ResumeLayout(false);
            this.mpGrpScanProgress.PerformLayout();
            this.ResumeLayout(false);

    }

    #endregion

    private System.Windows.Forms.TabControl tabControl1;
    private System.Windows.Forms.TabPage tabPageScan;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel5;
    private System.Windows.Forms.TextBox textBoxBandwidth;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel4;
    private System.Windows.Forms.TextBox textBoxFreq;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel2;
    private System.Windows.Forms.CheckBox checkBoxCreateGroups;
    private System.Windows.Forms.ListView listViewStatus;
    private System.Windows.Forms.ColumnHeader columnHeader1;
    private System.Windows.Forms.ProgressBar progressBarQuality;
    private System.Windows.Forms.ProgressBar progressBarLevel;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.ProgressBar progressBar1;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel3;
    private MediaPortal.UserInterface.Controls.MPButton mpButtonScanTv;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel1;
    private MediaPortal.UserInterface.Controls.MPComboBox mpComboBoxCountry;
    private System.Windows.Forms.TabPage tabPageCIMenu;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel6;
    private MediaPortal.UserInterface.Controls.MPComboBox mpComboBoxRegion;
    private System.Windows.Forms.GroupBox groupBox2;
    private MediaPortal.UserInterface.Controls.MPGroupBox mpGrpScanProgress;
    private MediaPortal.UserInterface.Controls.MPGroupBox mpGrpAdvancedTuning;
    private System.Windows.Forms.CheckBox checkBoxAdvancedTuning;
    private System.Windows.Forms.RadioButton scanNIT;
    private System.Windows.Forms.RadioButton scanSingleTransponder;
    private System.Windows.Forms.RadioButton scanPredefProvider;
    private System.Windows.Forms.CheckBox checkBoxCreateSignalGroup;
    private System.Windows.Forms.CheckBox checkBoxEnableChannelMoveDetection;
    private MediaPortal.UserInterface.Controls.MPLabel mpLabel7;
    private System.Windows.Forms.CheckBox checkBoxEnableStrongestChannelSelection;
  }
}