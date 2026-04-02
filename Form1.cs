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
            sfAIAssistView1.DataBindings.Add("Suggestions", viewModel, "Suggestions", true, DataSourceUpdateMode.OnPropertyChanged);

            if (viewModel.CurrentUser != null && !string.IsNullOrEmpty(sfAIAssistView1.User.Name))
            {
                viewModel.CurrentUser = sfAIAssistView1.User;
            }
            else
            {
                sfAIAssistView1.User = viewModel.CurrentUser;
            }
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
            if (e.Item is ReservationSuggestion suggestion)
            {
                if (suggestion.Action == SuggestionAction.ShowDatePicker)
                {
                    viewModel.AddInteractivelaceholder("Please Select a Date...");
                    var message = viewModel.Chats[viewModel.Chats.Count - 1] as TextMessage;

                    DateTimePicker dateTime = new DateTimePicker
                    {
                        Format = DateTimePickerFormat.Short,
                        Dock = DockStyle.None,
                    };

                    dateTime.CloseUp += (s, ev) =>
                    {
                        string result = dateTime.Value.ToShortDateString();
                        sfAIAssistView1.SetUserView(message, null);
                        viewModel.Chats.Remove(message);
                        viewModel.SendUserMessage(result);
                    };
                    sfAIAssistView1.SetUserView(message, dateTime);
                }
                else if (suggestion.Action == SuggestionAction.ShowNumberPicker)
                {
                    viewModel.AddInteractivelaceholder("Select number of days...");
                    var message = viewModel.Chats[viewModel.Chats.Count - 1] as TextMessage;

                    NumericUpDown dayPicker = new NumericUpDown
                    {
                        Minimum = 1,
                        Maximum = 30,
                        Width = 100,
                        Dock = DockStyle.None,
                    };

                    dayPicker.KeyDown += (s, ev) => {
                        if (ev.KeyCode == Keys.Enter)
                        {
                            string result = dayPicker.Value.ToString();
                            sfAIAssistView1.SetUserView(message, null);
                            viewModel.Chats.Remove(message);
                            viewModel.SendUserMessage(result);
                        }
                    };

                    sfAIAssistView1.SetUserView(message, dayPicker);
                    this.BeginInvoke(new MethodInvoker(() => dayPicker.Focus()));
                }
                else if (suggestion.Action == SuggestionAction.ShowComboSelector)
                {
                    // show a combo box populated with airline names when the suggestion is the airline selector; otherwise
                    // show airline names, class names, or country names depending on the suggestion label.
                    var display = suggestion.DisplayText ?? string.Empty;
                    var isAirline = display.IndexOf("airline", StringComparison.InvariantCultureIgnoreCase) >= 0;
                    var isClass = display.IndexOf("class", StringComparison.InvariantCultureIgnoreCase) >= 0;

                    viewModel.AddInteractivelaceholder(isAirline ? "Please choose an airline..." : "Please choose a country...");
                    var message = viewModel.Chats[viewModel.Chats.Count - 1] as TextMessage;

                    ComboBox combo = new ComboBox
                    {
                        Dock = DockStyle.None,
                        Width = 200,
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        DataSource = isAirline
                                ? Enum.GetNames(typeof(Airline)).ToList()
                                : isClass
                                    ? Enum.GetNames(typeof(Class)).ToList()
                                    : ResponseManager.Instance.Countries.Select(c => c.Name).ToList()
                    };

                    combo.SelectionChangeCommitted += (s, ev) =>
                    {
                        if (combo.SelectedItem != null)
                        {
                            string selected = combo.SelectedItem.ToString();
                            sfAIAssistView1.SetUserView(message, null);
                            viewModel.Chats.Remove(message);
                            // Send the exact selected string so TrySetValue can match the enum
                            viewModel.SendUserMessage(selected);
                        }
                    };

                    sfAIAssistView1.SetUserView(message, combo);
                    this.BeginInvoke(new MethodInvoker(() => combo.DroppedDown = true));
                }
                else
                {
                    viewModel.SendUserMessage(suggestion.DisplayText);
                }
            }
            else if (e.Item is string plainString)
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

            // Bot message label
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = Color.FromArgb(230, 240, 255),
                ForeColor = Color.FromArgb(24, 24, 24),
                Padding = new Padding(5),
                Margin = new Padding(0, 0, 0, 6)
            };
            return lbl;
        }

        // Creates a user message view, adapting UI depending on the message type
        private Control CreateUserView(TextMessage message)
        {
            string text = string.IsNullOrEmpty(message?.Text) ? string.Empty : message.Text.Trim();

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

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowTypingIndicator")
            {
                sfAIAssistView1.ShowTypingIndicator = viewModel.ShowTypingIndicator;
            }
        }
    }
}
