using Mediaportal.TV.Server.SetupControls.UserInterfaceControls;

namespace Mediaportal.TV.Server.SetupTV.Dialogs
{
  partial class FormEditTuningDetailCapture
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
      this.labelIsVcrSignal = new Mediaportal.TV.Server.SetupControls.UserInterfaceControls.MPLabel();
      this.checkBoxIsVcrSignal = new Mediaportal.TV.Server.SetupControls.UserInterfaceControls.MPCheckBox();
      this.comboBoxAudioSource = new Mediaportal.TV.Server.SetupControls.UserInterfaceControls.MPComboBox();
      this.labelAudioSource = new Mediaportal.TV.Server.SetupControls.UserInterfaceControls.MPLabel();
      this.comboBoxVideoSource = new Mediaportal.TV.Server.SetupControls.UserInterfaceControls.MPComboBox();
      this.labelVideoSource = new Mediaportal.TV.Server.SetupControls.UserInterfaceControls.MPLabel();
      this.SuspendLayout();
      // 
      // buttonCancel
      // 
      this.buttonCancel.Location = new System.Drawing.Point(198, 243);
      this.buttonCancel.TabIndex = 19;
      // 
      // buttonOkay
      // 
      this.buttonOkay.Location = new System.Drawing.Point(103, 243);
      this.buttonOkay.TabIndex = 18;
      // 
      // labelIsVcrSignal
      // 
      this.labelIsVcrSignal.AutoSize = true;
      this.labelIsVcrSignal.Location = new System.Drawing.Point(12, 215);
      this.labelIsVcrSignal.Name = "labelIsVcrSignal";
      this.labelIsVcrSignal.Size = new System.Drawing.Size(62, 13);
      this.labelIsVcrSignal.TabIndex = 16;
      this.labelIsVcrSignal.Text = "VCR signal:";
      // 
      // checkBoxIsVcrSignal
      // 
      this.checkBoxIsVcrSignal.AutoSize = true;
      this.checkBoxIsVcrSignal.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
      this.checkBoxIsVcrSignal.Location = new System.Drawing.Point(123, 213);
      this.checkBoxIsVcrSignal.Name = "checkBoxIsVcrSignal";
      this.checkBoxIsVcrSignal.Size = new System.Drawing.Size(27, 17);
      this.checkBoxIsVcrSignal.TabIndex = 17;
      this.checkBoxIsVcrSignal.TabStop = false;
      this.checkBoxIsVcrSignal.Text = " ";
      this.checkBoxIsVcrSignal.UseVisualStyleBackColor = true;
      // 
      // comboBoxAudioSource
      // 
      this.comboBoxAudioSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.comboBoxAudioSource.FormattingEnabled = true;
      this.comboBoxAudioSource.Location = new System.Drawing.Point(123, 186);
      this.comboBoxAudioSource.Name = "comboBoxAudioSource";
      this.comboBoxAudioSource.Size = new System.Drawing.Size(150, 21);
      this.comboBoxAudioSource.TabIndex = 15;
      // 
      // labelAudioSource
      // 
      this.labelAudioSource.AutoSize = true;
      this.labelAudioSource.Location = new System.Drawing.Point(12, 189);
      this.labelAudioSource.Name = "labelAudioSource";
      this.labelAudioSource.Size = new System.Drawing.Size(72, 13);
      this.labelAudioSource.TabIndex = 14;
      this.labelAudioSource.Text = "Audio source:";
      // 
      // comboBoxVideoSource
      // 
      this.comboBoxVideoSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.comboBoxVideoSource.FormattingEnabled = true;
      this.comboBoxVideoSource.Location = new System.Drawing.Point(123, 159);
      this.comboBoxVideoSource.Name = "comboBoxVideoSource";
      this.comboBoxVideoSource.Size = new System.Drawing.Size(150, 21);
      this.comboBoxVideoSource.TabIndex = 13;
      // 
      // labelVideoSource
      // 
      this.labelVideoSource.AutoSize = true;
      this.labelVideoSource.Location = new System.Drawing.Point(12, 162);
      this.labelVideoSource.Name = "labelVideoSource";
      this.labelVideoSource.Size = new System.Drawing.Size(72, 13);
      this.labelVideoSource.TabIndex = 12;
      this.labelVideoSource.Text = "Video source:";
      // 
      // FormEditTuningDetailCapture
      // 
      this.AcceptButton = this.buttonOkay;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.CancelButton = this.buttonCancel;
      this.ClientSize = new System.Drawing.Size(285, 278);
      this.Controls.Add(this.labelIsVcrSignal);
      this.Controls.Add(this.comboBoxAudioSource);
      this.Controls.Add(this.checkBoxIsVcrSignal);
      this.Controls.Add(this.labelAudioSource);
      this.Controls.Add(this.comboBoxVideoSource);
      this.Controls.Add(this.labelVideoSource);
      this.Name = "FormEditTuningDetailCapture";
      this.Text = "Add/Edit Capture Tuning Detail";
      this.Controls.SetChildIndex(this.checkBoxIsHighDefinition, 0);
      this.Controls.SetChildIndex(this.labelIsHighDefinition, 0);
      this.Controls.SetChildIndex(this.checkBoxIsThreeDimensional, 0);
      this.Controls.SetChildIndex(this.labelIsThreeDimensional, 0);
      this.Controls.SetChildIndex(this.labelIsEncrypted, 0);
      this.Controls.SetChildIndex(this.textBoxProvider, 0);
      this.Controls.SetChildIndex(this.labelProvider, 0);
      this.Controls.SetChildIndex(this.checkBoxIsEncrypted, 0);
      this.Controls.SetChildIndex(this.textBoxName, 0);
      this.Controls.SetChildIndex(this.labelName, 0);
      this.Controls.SetChildIndex(this.textBoxNumber, 0);
      this.Controls.SetChildIndex(this.labelVideoSource, 0);
      this.Controls.SetChildIndex(this.comboBoxVideoSource, 0);
      this.Controls.SetChildIndex(this.labelNumber, 0);
      this.Controls.SetChildIndex(this.labelAudioSource, 0);
      this.Controls.SetChildIndex(this.buttonOkay, 0);
      this.Controls.SetChildIndex(this.checkBoxIsVcrSignal, 0);
      this.Controls.SetChildIndex(this.buttonCancel, 0);
      this.Controls.SetChildIndex(this.comboBoxAudioSource, 0);
      this.Controls.SetChildIndex(this.labelIsVcrSignal, 0);
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private MPLabel labelIsVcrSignal;
    private MPCheckBox checkBoxIsVcrSignal;
    private MPComboBox comboBoxAudioSource;
    private MPLabel labelAudioSource;
    private MPComboBox comboBoxVideoSource;
    private MPLabel labelVideoSource;
  }
}