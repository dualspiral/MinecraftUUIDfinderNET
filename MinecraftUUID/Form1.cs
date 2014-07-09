// (c) Dr Daniel Naylor, 2014. Minecraft is (c) Mojang.
// Feel free to use this under the MIT license. 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MinecraftUUID.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MinecraftUUID
{
    /// <summary>
    /// Handles the logic for the main form.
    /// </summary>
    /// <remarks>
    /// Disclaimer: This is not my best coding. This is a simple utility built for a friend which I
    /// thought I would share.
    /// </remarks>
    public partial class Form1 : Form
    {
        private readonly Regex usernameRegex = new Regex(@"^[a-zA-Z0-9_]{1,16}$");
        private bool _isExecuting;
        
        /// <summary>
        /// Gets or sets a value indicating whether the user wants dashes in their UUID.
        /// </summary>
        private bool dashes 
        {
            get
            {
                return Settings.Default.dashes;
            }

            set
            {
                Settings.Default.dashes = value;
                showDashesToolStripMenuItem.Checked = value;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a request is ongoing.
        /// </summary>
        private bool IsExecuting
        {
            get
            {
                return _isExecuting;
            }

            set
            {
                _isExecuting = value;
                textBox1.Enabled = !_isExecuting;
            }
        }

        /// <summary>
        /// Constructs the form, and initialises the settings.
        /// </summary>
        public Form1()
        {
            InitializeComponent();
            if (!Settings.Default.upgraded)
            {
                Settings.Default.Upgrade();
                Settings.Default.upgraded = true;
            }

            showDashesToolStripMenuItem.Checked = dashes;
        }

        /// <summary>
        /// Fires when the text in the text box changes. Checks to see if the username is valid,
        /// and changes the enabled state of the button.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/></param>
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = !IsExecuting && usernameRegex.IsMatch(textBox1.Text);
        }

        /// <summary>
        /// Fires when the "Get UUID" button is clicked.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/></param>
        private void button1_Click(object sender, EventArgs e)
        {
            // If we are already executing a request, don't start another one.
            if (IsExecuting)
            {
                return;
            }

            IsExecuting = true;

            var request = WebRequest.Create("https://api.mojang.com/profiles/minecraft") as HttpWebRequest;

            request.ContentType = "application/json";
            request.Method = "POST";

            // The body must be in JSON format, as an array of names. As this utility only checks for one,
            // we just wrap our string in ["..."].
            var body = string.Format("[ \"{0}\" ]", textBox1.Text);
            using (var rStream = new StreamWriter(request.GetRequestStream()))
            {
                rStream.Write(body);
            }

            using (var re = request.GetResponse())
            {
                try
                {
                    string response;

                    // Read the stream to the end to get the whole content body.
                    using (var st = new StreamReader(re.GetResponseStream()))
                    {
                        response = st.ReadToEnd();
                    }

                    // We expect an array object, so turn it into a .NET list we can read.
                    var l = JsonConvert.DeserializeObject<List<object>>(response);

                    if (l.Count == 0)
                    {
                        // Empty list - no user!
                        setError(true);
                        return;
                    }

                    // We are only expecting one object here - get a dictionary out of it.
                    var r = (l.First() as JObject).ToObject<Dictionary<string, object>>();

                    if (r.ContainsKey("id"))
                    {
                        try
                        {
                            // Legacy only appears if it is true.
                            var isLegacy = r.ContainsKey("legacy") && (bool)r["legacy"];
                            setSuccess(r["name"] as string, r["id"] as string, isLegacy);
                        }
                        catch
                        {
                            setError(false);
                        }

                        return;
                    }

                    setError(false);
                }
                finally
                {
                    IsExecuting = false;
                    textBox1.Focus();
                }
            }
        }

        private void setError(bool ok)
        {
            uuid.Text = string.Empty;
            userName.Text = ok ? "User not found" : "An error occured";
            legacy.Text = string.Empty;
        }

        private void setSuccess(string name, string uuid, bool isLegacy)
        {
            userName.Text = name;
            string u = uuid;

            if (dashes)
            {
                // Parse the string as a GUID, then return it to string form with dashes.
                u = Guid.Parse(uuid).ToString("D");
            }

            this.uuid.Text = u;
            legacy.Text = isLegacy ? "Yes" : "No";
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var w = new AboutBox1();
            w.ShowDialog();
        }

        private void webVersionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Run this async, sometimes waiting for the web browser to return can block the main thread.
            var task = new Task(() =>
            {
                Process.Start("http://drnaylor.co.uk/uuid");
            });

            task.Start();
        }

        private void showDashesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Flip the dashes bit!
            dashes = !dashes;
        }

    }
}
