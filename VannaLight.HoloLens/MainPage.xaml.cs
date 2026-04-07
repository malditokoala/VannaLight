using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace VannaLight.HoloLens
{
    public sealed partial class MainPage : Page
    {
        private const string DefaultFrontendUrl = "http://192.168.0.10:3000";
        private const string DefaultApiBaseUrl = "http://192.168.0.10:5000";
        private AppRuntimeSettings runtimeSettings = new();

        public MainPage()
        {
            InitializeComponent();
            Loaded += MainPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            runtimeSettings = AppRuntimeSettings.Load();
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_Loaded;
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            await ShellWebView.EnsureCoreWebView2Async();

            ShellWebView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
            ShellWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            ShellWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            ShellWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            await ShellWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildBootstrapScript());

            if (!Uri.TryCreate(runtimeSettings.FrontendUrl, UriKind.Absolute, out var frontendUri))
            {
                frontendUri = new Uri(DefaultFrontendUrl);
            }

            ShellWebView.Source = frontendUri;
        }

        private void CoreWebView2_PermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
        {
            if (args.PermissionKind == CoreWebView2PermissionKind.Camera ||
                args.PermissionKind == CoreWebView2PermissionKind.Microphone)
            {
                args.State = CoreWebView2PermissionState.Allow;
                args.Handled = true;
            }
        }

        private string BuildBootstrapScript()
        {
            var apiUrl = JsonSerializer.Serialize(runtimeSettings.ApiBaseUrl);
            var frontendUrl = JsonSerializer.Serialize(runtimeSettings.FrontendUrl);

            return $@"
                (() => {{
                    window.API_URL = {apiUrl};
                    window.API_BASE_URL = {apiUrl};
                    window.VANNALIGHT_CONFIG = Object.freeze({{
                        apiBaseUrl: {apiUrl},
                        frontendUrl: {frontendUrl}
                    }});
                }})();
            ";
        }

        private sealed class AppRuntimeSettings
        {
            public string FrontendUrl { get; set; } = DefaultFrontendUrl;

            public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

            public static AppRuntimeSettings Load()
            {
                try
                {
                    var file = Package.Current.InstalledLocation.GetFileAsync("appsettings.json").AsTask().GetAwaiter().GetResult();
                    var json = FileIO.ReadTextAsync(file).AsTask().GetAwaiter().GetResult();
                    var settings = JsonSerializer.Deserialize<AppRuntimeSettings>(json);

                    return settings ?? new AppRuntimeSettings();
                }
                catch
                {
                    return new AppRuntimeSettings();
                }
            }
        }
    }
}
