using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Newtonsoft.Json;
using ReactiveUI;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web;
using static SpotifyAPI.Web.Scopes;

namespace FastDownloader.GUI
{
    public partial class MainWindow : Window
    {

        private EmbedIOAuthServer _server;
        private const string _clientId = "31407b8b381c4e0ebf7b8fafb03e2615";
        private const string CredentialsPath = "credentials.json";
        private SpotifyClient spotify;

        public MainWindow()
        {
            InitializeComponent();
            LoginSpotifyCommand = ReactiveCommand.Create(LoginSpotify);
        }

        public ICommand LoginSpotifyCommand { get; }

        private async Task LoginSpotify()
        {
            _server ??= new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);

            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            await _server.Start();
            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await _server.Stop();
                PKCETokenResponse token = await new OAuthClient().RequestToken(
                    new PKCETokenRequest(_clientId, response.Code, _server.BaseUri, verifier)
                );

                await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
                await Start();
            };

            var request = new LoginRequest(_server.BaseUri, _clientId, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = challenge,
                CodeChallengeMethod = "S256",
                Scope = new List<string> { UserReadEmail, UserReadPrivate, PlaylistReadPrivate, PlaylistReadCollaborative }
            };

            Uri uri = request.ToUri();
            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to open URL, manually open: {0}", uri);
            }
        }

        private async Task Start()
        {
            var json = await File.ReadAllTextAsync(CredentialsPath);
            var token = JsonConvert.DeserializeObject<PKCETokenResponse>(json);

            var authenticator = new PKCEAuthenticator(_clientId, token!);
            authenticator.TokenRefreshed += (sender, token) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(token));

            var config = SpotifyClientConfig.CreateDefault()
                .WithAuthenticator(authenticator);

            spotify = new SpotifyClient(config);

            var me = await spotify.UserProfile.Current();
            Console.WriteLine($"Bem vindo {me.DisplayName} ({me.Id}), você foi autenticado no Spotify!");

            var playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
            Console.WriteLine($"Playlists na sua conta: {playlists.Count}");
        }

        private static string GetSpotifyId(string url)
        {
            var startIndex = url.LastIndexOf("/", StringComparison.Ordinal);
            var endIndex = url.LastIndexOf("?", StringComparison.Ordinal) - 1 - url.LastIndexOf("/", StringComparison.Ordinal);

            return url.Substring(startIndex + 1, endIndex);
        }

    }
}