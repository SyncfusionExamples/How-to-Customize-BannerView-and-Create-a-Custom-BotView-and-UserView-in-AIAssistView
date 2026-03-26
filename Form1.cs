using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.WinForms.AIAssistView;
using System.Collections.Specialized;
using System.Windows.Forms.Integration;
using System.Windows;
using AISettingsWindow;
using System.Runtime.Versioning;

namespace AssistViewDemo
{
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    public partial class Form1 : Form
    {
        AIAssistViewModel viewModel = new AIAssistViewModel();
        private SfAIAssistView sfAIAssistView1;
        private Dictionary<TextMessage, ElementHost> botControlMapping = new Dictionary<TextMessage, ElementHost>();  

        public Form1()
        {

            InitializeComponent();
            sfAIAssistView1 = new SfAIAssistView();
            sfAIAssistView1.Location = new System.Drawing.Point(41, 40);
            sfAIAssistView1.Size = new System.Drawing.Size(818, 457);
            sfAIAssistView1.Dock = DockStyle.Fill;
            this.Controls.Add(sfAIAssistView1);

            sfAIAssistView1.DataBindings.Add("Messages", viewModel, "Chats", true, DataSourceUpdateMode.OnPropertyChanged);
            sfAIAssistView1.DataBindings.Add("ShowTypingIndicator", viewModel, "ShowTypingIndicator", true, DataSourceUpdateMode.OnPropertyChanged);
            sfAIAssistView1.DataBindings.Add("Suggestions", viewModel, "Suggestion", true, DataSourceUpdateMode.OnPropertyChanged);
            viewModel.CurrentUser = sfAIAssistView1.User;

            viewModel.InitAI();

            sfAIAssistView1.ShowTypingIndicator = viewModel.ShowTypingIndicator;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            sfAIAssistView1.SuggestionSelected += OnSuggestionSelected;

            //To Apply custom views to any existing default messages
            foreach (var item in viewModel.Chats)
            {
                if (item is TextMessage tm)
                {
                    if (tm.Author?.Name == viewModel.CurrentUser?.Name)
                        sfAIAssistView1.SetUserView(tm, CreateUserView(tm));
                    else
                        sfAIAssistView1.SetBotView(tm, CreateBotView(tm));
                }
            }

            BannerTemplate();

            sfAIAssistView1.TypingIndicator.Author = new Author() { Name = "Bot", AvatarImage = Image.FromFile(@"Asset\AI_Assist.png") };
            sfAIAssistView1.TypingIndicator.DisplayText = "Typing";    
            viewModel.Chats.CollectionChanged += Chats_CollectionChanged;
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            viewModel.Chats.CollectionChanged -= Chats_CollectionChanged;
          
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            foreach (var kvp in botControlMapping)
            {
                if (kvp.Value is ElementHost host)
                {
                    if (host.Child is IDisposable disposableChild)
                        disposableChild.Dispose();
                    host.Child = null;
                    host.Dispose();
                }
            }
            botControlMapping.Clear();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ShowAISettings();
        }

        private void ShowAISettings()
        {
            var demoViewModel = new DemoBrowserViewModel();
            demoViewModel.EndPoint = AISettings.EndPoint;
            demoViewModel.ModelName = AISettings.ModelName;
            demoViewModel.Key = AISettings.Key;

            var settingsForm = new AISettingsForm(demoViewModel);
            settingsForm.StartPosition = FormStartPosition.CenterParent;
            if (settingsForm.ShowDialog(this) == DialogResult.OK)
            {
                viewModel.UpdateAI(demoViewModel.Key, demoViewModel.ModelName, demoViewModel.EndPoint);
            }
        }

        private void Chats_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;

            foreach (var newItem in e.NewItems ?? new object[0])
            {
                if (newItem is TextMessage message)
                {
                    // Map view based on author: user messages get user view, others get bot view
                    if (message.Author?.Name == viewModel.CurrentUser?.Name)
                    {
                        sfAIAssistView1.SetUserView(message, CreateUserView(message));
                    }
                    else
                    {
                        sfAIAssistView1.SetBotView(message, CreateBotView(message));
                    }
                }
            }
        }

        private void OnSuggestionSelected(object sender, SuggestionSelectedEventArgs e)
        {
            if (e.Item is string plainString)
            {
                viewModel.SendUserMessage(plainString);
            }
        }

        public bool isUpdating = false;

        // Configures and applies a custom banner style to the AI Assist view
        private void BannerTemplate()
        {

            BannerStyle customStyle = new BannerStyle
            {
                TitleFont = new Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold),
                SubTitleFont = new Font("Segoe UI", 12F, System.Drawing.FontStyle.Italic),
                ImageSize = AvatarSize.Medium,
                SubTitleColor = Color.Red,
                TitleColor = Color.Green,
            };

            string title = "AI Assist";
            string subTitle = "Your best AI Companion";

            // Apply banner with title, subtitle, image, and custom style
            sfAIAssistView1.SetBannerView(title, subTitle, Image.FromFile(@"Asset\AI_Assist.png"), customStyle);
        }

        // Creates a bot message view with text and interactive choice buttons
        private Control CreateBotView(TextMessage message)
        {
            string text = string.IsNullOrEmpty(message?.Text) ? "Hello from the bot." : message.Text;

            // Container for bot message
            var container = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = true,
                Padding = new Padding(6),
                BackColor = Color.Transparent
            };

            // Bot message label
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = Color.FromArgb(230, 240, 255),
                ForeColor = Color.FromArgb(24, 24, 24),
                Padding = new Padding(8),
                Margin = new Padding(0, 0, 0, 6)
            };

            container.Controls.Add(lbl);

            // Row for choice buttons
            var btnRow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0)
            };

            string[] choices = new[] { "Choose a TextBox to Type", "Choose a RichTextBox to Type" };
            foreach (var c in choices)
            {
                var btn = new Button
                {
                    Text = c,
                    AutoSize = true,
                    Tag = c,
                    BackColor = Color.WhiteSmoke,
                    Margin = new Padding(0, 0, 6, 0)
                };

                btn.Click += (s, e) =>
                {
                    try
                    {
                        var choice = (string)((Button)s).Tag;
                        viewModel?.Chats.Add(new TextMessage
                        {
                            Author = viewModel.CurrentUser,
                            DateTime = DateTime.Now,
                            Text = choice
                        });
                    }
                    catch { }
                };

                btnRow.Controls.Add(btn);
            }

            container.Controls.Add(btnRow);

            return container;
        }

        // Creates a user message view, adapting UI depending on the message type
        private Control CreateUserView(TextMessage message)
        {
            string text = string.IsNullOrEmpty(message?.Text) ? string.Empty : message.Text.Trim();

            // Base container for user messages
            var container = new FlowLayoutPanel
            {
                AutoSize = false,
                WrapContents = true,
                Padding = new Padding(6),
                Height = 10,
                BackColor = Color.LightGreen
            };

            // Check if message is a special choice
            bool isChooseTextBox = string.Equals(text, "Choose a TextBox to Type", StringComparison.OrdinalIgnoreCase);
            bool isChooseRichTextBox = string.Equals(text, "Choose a RichTextBox to Type", StringComparison.OrdinalIgnoreCase);

            // For normal user messages, show a simple label
            if (!isChooseTextBox && !isChooseRichTextBox)
            {
                var lbl = new Label
                {
                    Text = text,
                    AutoSize = true,
                    BackColor = Color.LightGoldenrodYellow,
                    ForeColor = Color.Blue,
                    Padding = new Padding(5),
                    Margin = new Padding(0, 0, 0, 6)
                };
                return lbl;
            }


            // Choose TextBox to Type -> show a TextBox; Enter commits the message
            if (isChooseTextBox)
            {
                var row = new FlowLayoutPanel { AutoSize = true };
                var tb = new TextBox { Width = 240 };
                tb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        try
                        {
                            var value = tb.Text?.Trim();
                            if (!string.IsNullOrEmpty(value))
                            {
                                viewModel?.Chats.Add(new TextMessage
                                {
                                    Author = viewModel.CurrentUser,
                                    DateTime = DateTime.Now,
                                    Text = value
                                });
                                tb.Text = string.Empty;
                            }
                        }
                        catch { }
                    }
                };
                row.Controls.Add(tb);
                container.Controls.Add(row);
                return container;
            }

            // Choose RichTextBox to Type -> show a RichTextBox; Ctrl+Enter commits the message
            if (isChooseRichTextBox)
            {
                var row = new FlowLayoutPanel { AutoSize = true };
                var rtb = new RichTextBox { Width = 360 };
                rtb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && e.Control)
                    {
                        e.SuppressKeyPress = true;
                        try
                        {
                            var value = rtb.Text?.Trim();
                            if (!string.IsNullOrEmpty(value))
                            {
                                viewModel?.Chats.Add(new TextMessage
                                {
                                    Author = viewModel.CurrentUser,
                                    DateTime = DateTime.Now,
                                    Text = value
                                });
                                rtb.Clear();
                            }
                        }
                        catch { }
                    }
                };
                row.Controls.Add(rtb);

                // Small hint below the editor
                var hint = new Label { Text = "(Ctrl+Enter to send)", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(4, 4, 0, 0) };

                container.Controls.Add(row);
                container.Controls.Add(hint);
                return container;
            }

            return container;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowTypingIndicator")
            {
                sfAIAssistView1.ShowTypingIndicator = viewModel.ShowTypingIndicator;
            }
        }
    }
}
