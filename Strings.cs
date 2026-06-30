namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// User-facing UI/CLI strings, Turkish by default. <see cref="LocManager"/> reflection-writes these
/// fields to <c>lang.tr.xml</c> when it is missing, and — for any other chosen language — overwrites
/// the fields from <c>lang.&lt;code&gt;.xml</c> (e.g. the shipped <c>lang.en.xml</c>).
///
/// To localize a new string: add a public static string field here and a matching
/// &lt;s name="FieldName"&gt; entry in lang.en.xml. Fields are mutable on purpose (reflection sets
/// them); never mark them readonly/const. Interpolated text uses {0},{1}… with string.Format.
/// </summary>
internal static class Strings
{
    // ---- main window ----
    public static string AppTitle = "YouTube Playlist Senkronizatörü";
    public static string TabAccount = "🔑  Hesap";
    public static string TabPlaylists = "🎵  Çalma Listeleri";
    public static string TabDownloads = "⬇  İndirmeler";
    public static string TabSettings = "⚙  Ayarlar";
    public static string TabLogs = "📜  Günlükler";

    // ---- tray ----
    public static string TrayOpen = "Aç";
    public static string TrayExit = "Çıkış";
    public static string TraySyncing = "Arka planda senkronize ediliyor… açmak için çift tıklayın.";
    public static string TrayIdle = "YouTube Playlist Senkronizatörü";

    // ---- common buttons ----
    public static string BtnOk = "Tamam";
    public static string BtnCancel = "İptal";
    public static string BtnClose = "Kapat";
    public static string BtnSave = "Kaydet";
    public static string BtnBrowse = "Gözat…";
    public static string BtnRefresh = "Yenile";

    // ---- account / auth tab ----
    public static string AuthHeader = "Google / YouTube Yetkilendirmesi";
    public static string AuthStatusSignedOutFormat = "Bağlı değil. Devam etmek için bir credential JSON dosyası seçin.";
    public static string AuthStatusCredentialLoaded = "Credential yüklendi. Tarayıcıyla giriş yapın veya bir refresh token girin.";
    public static string AuthStatusSignedInFormat = "Bağlı ✓  ({0})";
    public static string AuthPickCredential = "Credential JSON seç…";
    public static string AuthPasteRefresh = "Refresh token yapıştır…";
    public static string AuthSignIn = "Tarayıcıyla giriş yap (refresh token üret)";
    public static string AuthSignOut = "Bağlantıyı kes";
    public static string AuthCredentialFilter = "Credential dosyaları|*.json;*.enc|Tüm dosyalar|*.*";
    public static string AuthWaitingBrowser = "Tarayıcıda yetkilendirme bekleniyor…";
    public static string AuthSucceeded = "Yetkilendirme başarılı. Refresh token kaydedildi.";
    public static string AuthFailedFormat = "Yetkilendirme başarısız: {0}";
    public static string AuthNeedCredentialFirst = "Önce bir credential JSON dosyası seçin.";
    public static string AuthRefreshPrompt = "Refresh token (1// ile başlar):";

    // ---- playlists tab ----
    public static string PlaylistsHeader = "Hesabımdaki Çalma Listeleri";
    public static string PlaylistsLoad = "Çalma listelerini getir";
    public static string PlaylistsLoadingFormat = "Çalma listeleri yükleniyor…";
    public static string PlaylistsCountFormat = "{0} çalma listesi bulundu.";
    public static string PlaylistsColTitle = "Başlık";
    public static string PlaylistsColCount = "Video";
    public static string PlaylistsColTarget = "Hedef klasör";
    public static string PlaylistsColMode = "Tür";
    public static string PlaylistsPickTarget = "Hedef klasör seç…";
    public static string PlaylistsConfigure = "Ayarla…";
    public static string PlaylistsSyncSelected = "Seçilenleri senkronize et";
    public static string PlaylistsNeedAuth = "Çalma listelerini getirmek için önce Hesap sekmesinden giriş yapın.";
    public static string PlaylistsConfigureColumn = "Ayarla…";
    public static string PlaylistsNeedTargetFormat = "Şu listeler için önce 'Ayarla…' ile hedef klasör seçin:\n{0}";
    public static string PlaylistsNothingSelected = "Senkronize edilecek çalma listesi seçilmedi.";

    // ---- media / quality ----
    public static string MediaKindMusic = "Müzik";
    public static string MediaKindVideo = "Video";
    public static string QualityBest = "En iyi kalite";
    public static string QualityCustom = "Özel kalite…";
    public static string QualityWorst = "En düşük kalite (yer tasarrufu)";
    public static string QualityConvertCodec2 = "Codec2'ye çevir (ffmpeg)";
    public static string QualityVideoLabel = "Video kalitesi:";

    // ---- downloads tab ----
    public static string DownloadsHeader = "İndirme İlerlemesi";
    public static string DownloadsColItem = "Öğe";
    public static string DownloadsColStatus = "Durum";
    public static string DownloadsColProgress = "İlerleme";
    public static string DownloadsStart = "Başlat";
    public static string DownloadsCancel = "İptal";
    public static string DownloadsStatusScanning = "Hedef klasör taranıyor…";
    public static string DownloadsStatusEnumerating = "Çalma listesi okunuyor…";
    public static string DownloadsStatusDownloading = "İndiriliyor…";
    public static string DownloadsStatusConverting = "Dönüştürülüyor…";
    public static string DownloadsStatusDone = "✅ Tamamlandı";
    public static string DownloadsStatusSkippedLive = "⏭ Atlandı (canlı yayın)";
    public static string DownloadsStatusFailedFormat = "⚠ Hata: {0}";
    public static string DownloadsSummaryFormat = "İndirildi {0} • Atlandı {1} • Hata {2} • Zaten var {3}";

    // ---- settings tab ----
    public static string SettingsHeader = "Ayarlar";
    public static string SettingsLanguageLabel = "Dil:";
    public static string SettingsLanguageRestartNote = "Dil değişikliği uygulama yeniden başlatılınca tam uygulanır.";
    public static string SettingsLoggingLabel = "Günlüklemeyi etkinleştir";
    public static string SettingsCleanCacheLabel = "Senkronizasyondan sonra Cache klasörünü temizle";
    public static string SettingsSkipLiveLabel = "Canlı yayınları atla";
    public static string SettingsAutoUpdateYtDlpLabel = "Senkronizasyondan önce yt-dlp'yi güncelle";
    public static string SettingsConcurrencyLabel = "Eşzamanlı indirme:";

    // ---- logs tab ----
    public static string LogsCopy = "Günlükleri kopyala";
    public static string LogsClear = "Temizle";
    public static string LogsOpenFolder = "Günlük klasörünü aç";

    // ---- resilience / errors ----
    public static string RetryPromptFormat = "İşlem başarısız oldu:\n{0}\n\nYeniden denensin mi?";
    public static string FatalRestartPromptFormat = "Beklenmeyen bir hata oluştu:\n{0}\n\nUygulama yeniden başlatılıp denensin mi?";
    public static string UnexpectedErrorFormat = "Beklenmeyen hata:\n{0}";

    // ---- CLI ----
    public static string CliSyncStarting = "Senkronizasyon başlıyor (arka plan)…";
    public static string CliSyncDoneFormat = "Bitti. İndirildi {0}, hata {1}.";
    public static string CliNoProfiles = "Senkronize edilecek kayıtlı çalma listesi yok. Önce arayüzden ayarlayın.";
    public static string HelpTextFormat =
        "{0} v{1}\n\n" +
        "Kullanım:\n" +
        "  YoutubePlaylistSynchroniszer.exe [seçenekler]\n\n" +
        "Çift tıklayınca grafik arayüz açılır.\n\n" +
        "Seçenekler:\n" +
        "  --sync         Pencere açmadan, kayıtlı ayarlarla senkronize et (tepside simge), sonra kapan\n" +
        "  --gui          Grafik arayüzü aç\n" +
        "  -h, --help     Bu yardım\n" +
        "  -v, --version  Sürüm\n";
}
