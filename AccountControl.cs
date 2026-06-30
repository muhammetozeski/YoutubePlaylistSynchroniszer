namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The Account tab: import a Google client secret (.json/.enc), then either sign in via the browser to
/// mint a refresh token or paste/import an existing one. Shows connection status and can sign out.
/// </summary>
internal sealed class AccountControl : UserControl
{
    /// <summary>Raised whenever the authorization state changes (the Playlists tab listens to re-enable load).</summary>
    public event Action? AuthChanged;

    readonly Label _status = Ui.Label("");
    readonly Button _signInButton;

    public AccountControl()
    {
        Dock = DockStyle.Fill;

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
        layout.Controls.Add(Ui.Header(Strings.AuthHeader));
        layout.Controls.Add(_status);

        var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(Ui.Button(Strings.AuthPickCredential, (_, _) => PickCredential()));
        _signInButton = Ui.Button(Strings.AuthSignIn, (_, _) => SignIn(), primary: true);
        bar.Controls.Add(_signInButton);
        bar.Controls.Add(Ui.Button(Strings.AuthPasteRefresh, (_, _) => PasteRefresh()));
        bar.Controls.Add(Ui.Button(Strings.AuthSignOut, (_, _) => SignOut()));
        layout.Controls.Add(bar);

        Controls.Add(layout);
        RefreshStatus();
    }

    void PickCredential()
    {
        using var dialog = new OpenFileDialog { Title = Strings.AuthPickCredential, Filter = Strings.AuthCredentialFilter };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            CredentialStore.ImportClientSecret(dialog.FileName);
            AccessTokenProvider.Reset();
            RefreshStatus();
            AuthChanged?.Invoke();
        }
        catch (Exception ex) { NativeMessageBox.Error(string.Format(Strings.AuthFailedFormat, ex.Message)); }
    }

    void PasteRefresh()
    {
        if (!CredentialStore.HasClientSecret) { NativeMessageBox.Warn(Strings.AuthNeedCredentialFirst); return; }
        string? token = Prompt.ForText(Strings.AuthPasteRefresh, Strings.AuthRefreshPrompt);
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            CredentialStore.SetRefreshToken(token);
            AccessTokenProvider.Reset();
            RefreshStatus();
            AuthChanged?.Invoke();
        }
        catch (Exception ex) { NativeMessageBox.Error(string.Format(Strings.AuthFailedFormat, ex.Message)); }
    }

    async void SignIn()
    {
        if (!CredentialStore.HasClientSecret) { NativeMessageBox.Warn(Strings.AuthNeedCredentialFirst); return; }
        _signInButton.Enabled = false;
        _status.Text = Strings.AuthWaitingBrowser;

        await Resilience.GuardAsync("Browser sign-in", async () =>
        {
            var tokens = await GoogleOAuthService.AuthorizeAsync(CredentialStore.ClientSecret!);
            CredentialStore.SetRefreshToken(tokens.RefreshToken!);
            AccessTokenProvider.Reset();
            await CacheAccountLabelAsync();
            _status.Text = Strings.AuthSucceeded;
        });

        _signInButton.Enabled = true;
        RefreshStatus();
        AuthChanged?.Invoke();
    }

    void SignOut()
    {
        CredentialStore.Clear();
        AccessTokenProvider.Reset();
        Settings.AccountLabel.Value = "";
        SettingsManager.SaveSettings();
        RefreshStatus();
        AuthChanged?.Invoke();
    }

    static async Task CacheAccountLabelAsync()
    {
        string? label = await YouTubeApiClient.TryGetAccountLabelAsync();
        if (string.IsNullOrWhiteSpace(label)) return;
        Settings.AccountLabel.Value = label!;
        SettingsManager.SaveSettings();
    }

    void RefreshStatus()
    {
        _status.Text =
            CredentialStore.IsAuthorized
                ? string.Format(Strings.AuthStatusSignedInFormat, string.IsNullOrWhiteSpace(Settings.AccountLabel.Value) ? "YouTube" : Settings.AccountLabel.Value)
                : CredentialStore.HasClientSecret ? Strings.AuthStatusCredentialLoaded
                : Strings.AuthStatusSignedOutFormat;
    }
}
