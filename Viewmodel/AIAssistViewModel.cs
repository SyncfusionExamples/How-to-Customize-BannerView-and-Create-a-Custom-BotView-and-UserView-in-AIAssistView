using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;
using Syncfusion.WinForms.AIAssistView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using static AssistViewDemo.AIAssistViewModel;

namespace AssistViewDemo
{
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    public class AIAssistViewModel : INotifyPropertyChanged
    {
        #region Fields and property
       AIAssistChatService service;

        private ObservableCollection<object> chats;
        public ObservableCollection<object> Chats
        {
            get
            {
                return this.chats;
            }
            set
            {
                this.chats = value;
                RaisePropertyChanged("Chats");
            }
        }

        public void RaisePropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private Author currentUser;
        public Author CurrentUser
        {
            get
            {
                return this.currentUser;
            }
            set
            {
                this.currentUser = value;
                RaisePropertyChanged("CurrentUser");
            }
        }

        private bool showTypingIndicator;
        public bool ShowTypingIndicator
        {
            get
            {
                return this.showTypingIndicator;
            }
            set
            {
                this.showTypingIndicator = value;
                RaisePropertyChanged("ShowTypingIndicator");
            }
        }

        private IEnumerable<string> suggestion;
        public IEnumerable<string> Suggestion
        {
            get
            {
                if(this.suggestion == null)
                {
                    this.suggestion = new ObservableCollection<string>();
                }

                return this.suggestion;
            }
            set
            {
                this.suggestion = value;
                RaisePropertyChanged("Suggestion");
            }
        }

        private TypingIndicator typingIndicator;
        public TypingIndicator TypingIndicator
        {
            get
            {
                return this.typingIndicator;
            }
            set
            {
                this.typingIndicator = value;
                RaisePropertyChanged("TypingIndicator");
            }
        }

        #endregion

        public AIAssistViewModel()
        {
            this.Chats = new ObservableCollection<object>();
            this.Chats.CollectionChanged += Chats_CollectionChanged;
            this.Suggestion = new List<string>();
            this.CurrentUser = new Author() { Name = "John" };

            service = new AIAssistChatService();
            service.Initialize();

            Chats.Add(new TextMessage
            {
                Author = new Author { Name = "Bot", AvatarImage = Image.FromFile(@"Asset\AI_Assist.png") },
                DateTime = DateTime.Now,
                Text = "I am your AI assistant. \n" + "Please choose from the options below",
            });
        }

        public async void InitAI()
        {
            string goalSuggest = "How does WinForms handle UI rendering?";
            string goalSolution = "WinForms renders UI elements primarily by wrapping native Windows API (HWND-based) controls, using GDI/GDI+ for drawing, and firing Paint events for customization.";

            string toolSuggest = "What is the Future of WinForms?";
            string toolSolution = "The future of Windows Forms (WinForms) is primarily focused on maintenance, stability, and support for legacy applications running on modern .NET runtimes, rather than active development of new features or cross-platform expansion";

            service = new AIAssistChatService
            {
                OfflineContent = new Dictionary<string, string>
                {
                    { goalSuggest, goalSolution},
                    { toolSuggest, toolSolution}
                },
            };

            await service.Initialize();
            Suggestion = new List<string>(service.OfflineContent.Keys);
           
        }

        public void UpdateAI(string key, string model, string endpoint)
        {
            if (service != null)
            {
                service.UpdateAI(key, model, endpoint);
            }
        }

        public void SendUserMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            Chats.Add(new TextMessage
            {
                Author = this.currentUser,
                DateTime = DateTime.Now,
                Text = text,
            });
        }

        private bool skipNextNotify = false;

        public void AddInteractivelaceholder(string text)
        {
            skipNextNotify = true;
            Chats.Add(new TextMessage
            {
                Author = this.currentUser,
                DateTime = DateTime.Now,
                Text = text,
            });
            skipNextNotify = false;
        }

        private async void Chats_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (skipNextNotify || e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add) return;

            var item = e.NewItems?[0] as TextMessage;
            if (item != null)
            {
                if (item.Author.Name == currentUser.Name)
                {
                    var userText = (item.Text ?? string.Empty);

                    // If this is a client-side UI trigger, do not call the AI service here.
                    // The UI will present controls (calendar, place buttons, text box/label) and
                    // post bot confirmations or the actual user text when committed.
                    if (string.Equals(userText, "Choose a TextBox to Type", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(userText, "Choose a RichTextBox to Type", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    try
                    {
                        ShowTypingIndicator = true;

                        await service.NonStreamingChat(item.Text);

                        Chats.Add(new TextMessage
                        {
                            Author = new Author { Name = "Bot", AvatarImage = Image.FromFile(@"Asset\AI_Assist.png") },
                            DateTime = DateTime.Now,
                            Text = service.Response,
                        });
                    }
                    finally
                    {
                        ShowTypingIndicator = false;
                        if (service.Suggestion != null)
                            Suggestion = new List<string>(service.Suggestion);
                    }
                }
            }
        }

        public class AIAssistChatService
        {
            IChatCompletionService gpt;
            Kernel kernel;
            private string OPENAI_KEY = string.Empty; // configure for real AI service

            private string OPENAI_MODEL = string.Empty;

            private string API_ENDPOINT = string.Empty;

            public string Requirement { get; set; }
            public Dictionary<string, string> OfflineContent { get; set; } = new Dictionary<string, string>();

            private ObservableCollection<string> suggestion;
            public ObservableCollection<string> Suggestion
            {
                get
                {
                    if (this.suggestion == null)
                    {
                        this.suggestion = new ObservableCollection<string>();
                    }

                    return this.suggestion;
                }
                set
                {
                    this.suggestion = value;
                }
            }

            public string Response { get; set; }

            public void UpdateAI(string key, string model, string endpoint)
            {
                this.OPENAI_KEY = key;
                this.OPENAI_MODEL = model;
                this.API_ENDPOINT = endpoint;
                Initialize();
            }

            public async Task Initialize()
            {
                if (!string.IsNullOrWhiteSpace(OPENAI_KEY) &&
                    !string.IsNullOrWhiteSpace(OPENAI_MODEL) &&
                    !string.IsNullOrWhiteSpace(API_ENDPOINT))
                {
                    try
                    {
                        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
                        kernelBuilder.AddAzureOpenAIChatCompletion(
                            deploymentName: OPENAI_MODEL,
                            apiKey: OPENAI_KEY,
                            endpoint: API_ENDPOINT,
                            modelId: "gpt-4"
                        );

                        kernel = kernelBuilder.Build();
                        gpt = kernel.GetRequiredService<IChatCompletionService>();
                    }
                    catch (Exception)
                    {
                        // If initialization fails, gpt will remain null
                        gpt = null;
                    }
                }
            }


            public async Task NonStreamingChat(string line)
            {
                Response = string.Empty;
                Suggestion.Clear();

                // Check if credentials are missing or initialization failed
                if (string.IsNullOrWhiteSpace(OPENAI_KEY) ||
                    string.IsNullOrWhiteSpace(OPENAI_MODEL) ||
                    string.IsNullOrWhiteSpace(API_ENDPOINT) ||
                    gpt == null)
                {
                    await Task.Delay(1000); // Simulate network delay for realism
                    if (OfflineContent.ContainsKey(line))
                    {
                        Response = OfflineContent[line];
                    }
                    else
                    {
                        Response = "I'm sorry, I don't have an offline response for that. Try asking one of the suggested questions.";
                    }
                    
                    // Add some dummy suggestions for next steps
                    Suggestion.Add("How does WinForms handle UI rendering?");
                    Suggestion.Add("What is the Future of WinForms?");
                    return;
                }

                try
                {
                    var response = await gpt.GetChatMessageContentAsync(line);
                    Response = response.ToString();
                    // In a real scenario, you'd parseSuggestions from the AI response here
                }
                catch (Exception ex)
                {
                    Response = "Error connecting to AI service: " + ex.Message;
                }
            }

        }
    }
}
