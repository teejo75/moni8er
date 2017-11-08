using Moni8er.Logging;
using Moni8er.Process;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Moni8er
{
    public partial class MainForm : Form
    {
        // Used for factory reset
        private bool ConfigReset = false;

        #region System Menu Declarations

        private const int MF_SEPARATOR = 0x800;

        private const int MF_STRING = 0x0;

        // P/Invoke constants
        private const int WM_SYSCOMMAND = 0x112;

        // ID for the About item on the system menu
        private int SYSMENU_ABOUT_ID = 0x1;

        // ID for the Factory Reset item on the system menu
        private int SYSMENU_RESET_ID = 0x2;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

        // P/Invoke declarations
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool InsertMenu(IntPtr hMenu, int uPosition, int uFlags, int uIDNewItem, string lpNewItem);

        #endregion System Menu Declarations

        #region System Menu Methods

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Get a handle to a copy of this form's system (window) menu
            IntPtr hSysMenu = GetSystemMenu(this.Handle, false);

            // Add a separator
            AppendMenu(hSysMenu, MF_SEPARATOR, 0, string.Empty);

            // Add the About menu item
            AppendMenu(hSysMenu, MF_STRING, SYSMENU_ABOUT_ID, "&About…");

            // Add the Reset Config menu item
            AppendMenu(hSysMenu, MF_STRING, SYSMENU_RESET_ID, "Factory &Reset");
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            // Test if the About item was selected from the system menu
            if ((m.Msg == WM_SYSCOMMAND) && ((int)m.WParam == SYSMENU_ABOUT_ID))
            {
                AboutBox aboutBox = new AboutBox();
                aboutBox.ShowDialog();
                aboutBox.Dispose();
            }

            // Test if the Reset menu item was selected from the system menu
            if ((m.Msg == WM_SYSCOMMAND) && ((int)m.WParam == SYSMENU_RESET_ID))
            {
                if (MessageBox.Show("Are you sure that you want to reset to factory settings?", "Reset", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Properties.Settings.Default.Reset();
                    Properties.Settings.Default.Save();
                    ConfigReset = true;
                    Application.Restart();
                }
            }
        }

        #endregion System Menu Methods

        // Initialize Process library
        public Paths pp = new Paths();

        public MainForm()
        {
            InitializeComponent();
            // Receive events from the Process library
            pp.NotifyStatusEvent += pp_NotifyStatusEvent;
            pp.NotifyErrorEvent += pp_NotifyErrorEvent;
        }

        #region Events

        /// <summary>
        /// Event handler for Update library messages
        /// </summary>
        /// <param name="Message">Message string, if any</param>
        /// <param name="OldVersion">Old version string</param>
        /// <param name="NewVersion">New version string</param>
        private void updater_NotifyUpdateEvent(string Message, string OldVersion, string NewVersion)
        {
            if (MessageBox.Show("An update for Moni8er has been found.\n\nYour version: " + OldVersion + "\nNew Version: " + NewVersion + "\nMessage:\n" + Message + "\nVisit the download page?", "Updater", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                // Launches default browser to the download page if an update has been detected.
                System.Diagnostics.Process.Start("http://midnightreign.org/downloads/moni8er-a-mede8er-utility/");
            }
        }

        /// <summary>
        /// Listen for error events passed from the Process library
        /// </summary>
        /// <param name="ErrorText">Error string, if any</param>
        /// <param name="Aborted">If the current process has been aborted</param>
        private void pp_NotifyErrorEvent(string ErrorText, bool Aborted)
        {
            if (Aborted)
            {
                MessageBox.Show(ErrorText, "Aborted", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                DialogResult dr = MessageBox.Show(ErrorText, "Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (dr == DialogResult.Cancel) { pp.AbortProcess = true; }
            }
        }

        /// <summary>
        ///   Listen for progress update events from the Process library
        /// </summary>
        /// <param name="Progress">Current progress value</param>
        /// <param name="ProgressMax">Maximum progress value</param>
        /// <param name="NotifyText">Message updates</param>
        private void pp_NotifyStatusEvent(int Progress, int ProgressMax, string NotifyText)
        {
            toolStripProgressBar.Maximum = ProgressMax;
            toolStripProgressBar.Value = Progress;
            if (!String.IsNullOrEmpty(NotifyText))
            {
                toolStripStatusLabel.Text = NotifyText;
                this.Refresh(); // Form doesn't repaint if this is not called.
            };
        }

        #endregion Events

        #region Button Handlers

        /// <summary>
        /// Add paths to the listbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            // Instantiate new add dialog instead of invoking browser directly.
            AddDialog AddDlg = new AddDialog();

            DialogResult result = AddDlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                ListViewItem dupe = cbJukeboxList.FindItemWithText(AddDlg.SelectedPath);
                if (dupe == null)
                {
                    ListViewItem item = new ListViewItem();
                    item.Text = AddDlg.SelectedPath;
                    item.Checked = true;
                    cbJukeboxList.Items.Add(item);
                    if (Log.Logging) { Log.log.Log("Added: " + item.Text, LogFile.LogLevel.Info); }
                }
                else
                {
                    if (!cbJukeboxList.Items.Contains(dupe))
                    {
                        ListViewItem item = new ListViewItem();
                        item.Text = AddDlg.SelectedPath;
                        item.Checked = true;
                        cbJukeboxList.Items.Add(item);
                        if (Log.Logging) { Log.log.Log("Added: " + item.Text, LogFile.LogLevel.Info); }
                    }
                    else MessageBox.Show("This path is already in the list", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            AddDlg.Dispose();
        }

        /// <summary>
        /// Begins the Mede8er.db creation / update process.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonUpdate_Click(object sender, EventArgs e)
        {
            if (cbJukeboxList.CheckedItems.Count == 0)
            {
                MessageBox.Show("There are no folders to process", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                toolStripStatusLabel.Visible = true;
                toolStripProgressBar.Visible = true;
                toolStripStatusLabel.Text = "";
                this.Refresh(); // Update form display;
                cbJukeboxList.Enabled = !cbJukeboxList.Enabled;
                if (Log.Logging) { Log.log.Log("Processing folders to create databases", LogFile.LogLevel.Info); }
                foreach (ListViewItem item in cbJukeboxList.CheckedItems)
                {
                    pp.CreateUpdateDatabase(item.Text); // Call the Process Library to do its thing
                }
                toolStripProgressBar.Maximum = 100;
                toolStripProgressBar.Value = 0;
                if (!pp.AbortProcess)
                {
                    if (Log.Logging) { Log.log.Log("Complete!", LogFile.LogLevel.Info); }
                    toolStripStatusLabel.Text = "Complete!";
                }
                else
                {
                    if (Log.Logging) { Log.log.Log("Processing was cancelled! There may be an incomplete Mede8er.db file", LogFile.LogLevel.Warn); }
                    toolStripStatusLabel.Text = "Cancelled!";
                }
                cbJukeboxList.Enabled = !cbJukeboxList.Enabled;
            }
        }

        /// <summary>
        /// Remove entries from the listbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRemove_Click(object sender, EventArgs e)
        {
            if (cbJukeboxList.SelectedItems.Count == 0)
            {
                MessageBox.Show("There are no entries selected for removal", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                foreach (ListViewItem item in cbJukeboxList.SelectedItems)
                {
                    cbJukeboxList.Items.Remove(item);
                    if (Log.Logging) { Log.log.Log("Removed: " + item.Text, LogFile.LogLevel.Info); }
                }
            }
        }

        /// <summary>
        /// Toggle the logging checkbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbLog_CheckedChanged(object sender, EventArgs e)
        {
            if (cbLog.Checked) { if (!Log.Logging) { Log.startLogging(); } } else Log.stopLogging();
        }

        #endregion Button Handlers

        #region Form Events

        /// <summary>
        /// Save settings to disk on program exit.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfigReset) // Only do this if the Config hasn't been reset.
            {
                System.Collections.Specialized.StringCollection sc = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.MainFormSize = this.Size;
                Properties.Settings.Default.cbLogChecked = this.cbLog.Checked;

                if (cbJukeboxList.Items.Count != 0)
                {
                    foreach (ListViewItem item in cbJukeboxList.Items)
                    {
                        sc.Add(item.Text + "|" + item.Checked.ToString().ToLower());
                    }
                }
                if (sc.Count > 0)
                {
                    Properties.Settings.Default.jblItems = sc;
                }
                Properties.Settings.Default.Save();
            }

            if (Log.Logging)
            {
                Log.stopLogging();
            }
        }

        /// <summary>
        /// Handle settings file upgrades and settings load from disk on app startup.
        /// Also does basic component initialisation.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Copy settings to new build folder in %LOCALAPPDATA%
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            // Set the form title to the product name (Set in Application properties)
            this.Text = Application.ProductName;
            // Restore Form Size
            this.Size = Properties.Settings.Default.MainFormSize;
            // Restore the logging setting
            this.cbLog.Checked = Properties.Settings.Default.cbLogChecked;

            toolStripStatusLabel.Text = ""; // Blank out the design-time text

            // Set initial visibility false. The update button will set it to true.
            toolStripStatusLabel.Visible = false;
            toolStripProgressBar.Visible = false;
            // Configure the list box
            cbJukeboxList.Columns.Add("Path", cbJukeboxList.Width - 10, HorizontalAlignment.Left);

            // Restore the listbox that was saved to disk previously.
            System.Collections.Specialized.StringCollection sc = new System.Collections.Specialized.StringCollection();
            if (Properties.Settings.Default.jblItems != null)
            {
                sc = Properties.Settings.Default.jblItems;

                foreach (string scitem in sc)
                {
                    string[] spitem = scitem.Split('|');
                    ListViewItem lvitem = new ListViewItem();
                    lvitem.Text = spitem[0];
                    lvitem.Checked = Convert.ToBoolean(spitem[1]);
                    cbJukeboxList.Items.Add(lvitem);
                }
            }
            // Check if the directories loaded from disk still exist.
            if (cbJukeboxList.Items.Count != 0)
            {
                foreach (ListViewItem item in cbJukeboxList.Items)
                {
                    if (!Directory.Exists(item.Text))
                    {
                        item.ForeColor = System.Drawing.Color.Red; // Saved directory is no longer available. Make it red and uncheck it.
                        if (item.Checked)
                        {
                            item.Checked = false;
                        }
                    }
                    else if (item.ForeColor == System.Drawing.Color.Red) // If a previously unavailable folder is now back, revert the colour.
                    {
                        item.ForeColor = System.Drawing.SystemColors.WindowText;
                    }
                }
            }
        }

        #endregion Form Events
    }
}