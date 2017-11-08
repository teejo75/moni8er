using System;
using System.IO;
using System.Windows.Forms;

namespace Moni8er
{
    public partial class AddDialog : Form
    {
        public AddDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Property for the selected path, either from the browser or directly entered.
        /// </summary>
        public string SelectedPath
        {
            get
            {
                return Path.Text;
            }
        }

        /// <summary>
        /// Invoke the folder browser
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            folderBrowserDialog.Description = "Select the jukebox folder to add";
            folderBrowserDialog.ShowNewFolderButton = false;

            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                Path.Text = folderBrowserDialog.SelectedPath;
            }
        }

        /// <summary>
        /// Submit the form (Default handler for Enter key)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(Path.Text))
            {
                DialogResult = DialogResult.OK;
            }
            else MessageBox.Show("The specified path does not exist. Please correct it and try again.");
        }
    }
}