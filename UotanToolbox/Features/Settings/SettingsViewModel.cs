﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Newtonsoft.Json;
using Splat;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Enums;
using SukiUI.Models;
using SukiUI.Toasts;
using UotanToolbox.Common;

using UotanToolbox.Utilities;

namespace UotanToolbox.Features.Settings;

public partial class SettingsViewModel : MainPageBase
{
    public Action<SukiBackgroundStyle> BackgroundStyleChanged { get; set; }
    public Action<bool> BackgroundAnimationsChanged { get; set; }
    public Action<bool> BackgroundTransitionsChanged { get; set; }
    public Action<string> CustomBackgroundStyleChanged { get; set; }

    public IAvaloniaReadOnlyList<SukiColorTheme> AvailableColors { get; }
    public IAvaloniaReadOnlyList<SukiBackgroundStyle> AvailableBackgroundStyles { get; }
    public IAvaloniaReadOnlyList<string> CustomShaders { get; } = new AvaloniaList<string> { "Space", "Weird", "Clouds" };

    public AvaloniaList<string> LanguageList { get; } = [GetTranslation("Settings_Default"), "English", "简体中文"];
    [ObservableProperty] private string _selectedLanguageList;

    private readonly SukiTheme _theme = SukiTheme.GetInstance();

    [ObservableProperty] private bool _isLightTheme;
    [ObservableProperty] private SukiBackgroundStyle _backgroundStyle;
    [ObservableProperty] private bool _backgroundAnimations;
    [ObservableProperty] private bool _backgroundTransitions;
    [ObservableProperty] private string _currentVersion = Global.currentVersion;
    [ObservableProperty] private string _binVersion = null;
    public ISukiDialogManager DialogManager { get; }
    public ISukiToastManager ToastManager { get; }
    private string _customShader = null;

    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    public SettingsViewModel(ISukiDialogManager dialogManager, ISukiToastManager toastManager) : base(GetTranslation("Sidebar_Settings"), MaterialIconKind.SettingsOutline, -200)
    {
        DialogManager = dialogManager;
        ToastManager = toastManager;
        if (UotanToolbox.Settings.Default.Language is null or "")
        {
            SelectedLanguageList = GetTranslation("Settings_Default");
        }
        else if (UotanToolbox.Settings.Default.Language == "en-US")
        {
            SelectedLanguageList = "English";
        }
        else if (UotanToolbox.Settings.Default.Language == "zh-CN")
        {
            SelectedLanguageList = "简体中文";
        }

        _ = CheckBinVersion();
        AvailableBackgroundStyles = new AvaloniaList<SukiBackgroundStyle>(Enum.GetValues<SukiBackgroundStyle>());
        AvailableColors = _theme.ColorThemes;

        IsLightTheme = _theme.ActiveBaseTheme == ThemeVariant.Light;
        _theme.OnBaseThemeChanged += variant =>
            IsLightTheme = variant == ThemeVariant.Light;
        if (Global.isLightThemeChanged == false)
        {
            IsLightTheme = UotanToolbox.Settings.Default.IsLightTheme;
            Global.isLightThemeChanged = true;
        }
        _theme.OnColorThemeChanged += theme =>
        {
            // TODO: Implement a way to make this correct, might need to wrap the thing in a VM, this isn't ideal.
        };
    }

    public async Task CheckBinVersion()
    {
        BinVersion = await StringHelper.GetBinVersion();
    }

    partial void OnIsLightThemeChanged(bool value) =>
        _theme.ChangeBaseTheme(value ? ThemeVariant.Light : ThemeVariant.Dark);

    partial void OnSelectedLanguageListChanged(string value)
    {
        if (value == GetTranslation("Settings_Default")) UotanToolbox.Settings.Default.Language = "";
        else if (value == "English") UotanToolbox.Settings.Default.Language = "en-US";
        else if (value == "简体中文") UotanToolbox.Settings.Default.Language = "zh-CN";
        UotanToolbox.Settings.Default.Save();
        _ = ToastManager.CreateToast().WithTitle($"{GetTranslation("Settings_LanguageHasBeenSet")}").WithContent(GetTranslation("Settings_RestartTheApplication")).OfType(NotificationType.Success).Dismiss().ByClicking().Dismiss().After(TimeSpan.FromSeconds(3)).Queue();
    }

    [RelayCommand]
    private void SwitchToColorTheme(SukiColorTheme colorTheme)
    {
        _theme.ChangeColorTheme(colorTheme);
    }

    partial void OnBackgroundStyleChanged(SukiBackgroundStyle value) =>
        BackgroundStyleChanged?.Invoke(value);

    partial void OnBackgroundAnimationsChanged(bool value) =>
        BackgroundAnimationsChanged?.Invoke(value);

    partial void OnBackgroundTransitionsChanged(bool value) =>
        BackgroundTransitionsChanged?.Invoke(value);

    [RelayCommand]
    private void TryCustomShader(string shaderType)
    {
        _customShader = _customShader == shaderType ? null : shaderType;
        CustomBackgroundStyleChanged?.Invoke(_customShader);
    }

    [RelayCommand]
    private void OpenURL(string url)
    {
        UrlUtilities.OpenURL(url);
    }

    [RelayCommand]
    private async Task GetUpdate()
    {
        try
        {
            using HttpClient client = new HttpClient();
            string url = "https://toolbox.uotan.cn/api/list";
            StringContent content = new StringContent("{}", System.Text.Encoding.UTF8);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);
            _ = response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            dynamic convertedBody = JsonConvert.DeserializeObject<dynamic>(responseBody);
            SettingsViewModel vm = new SettingsViewModel(DialogManager, ToastManager);
            if (convertedBody.release_version != vm.CurrentVersion)
            {
                bool result = false;
                DialogManager.CreateDialog()
.WithTitle(GetTranslation("Settings_NewVersionAvailable"))
.WithContent((String)JsonConvert.SerializeObject(convertedBody.release_content))
.WithActionButton("Yes", _ => result = true, true)
.WithActionButton("No", _ => result = false, true)
.TryShow();
                if (result == true)
                {
                    UrlUtilities.OpenURL("https://toolbox.uotan.cn");
                }
            }
            else if (convertedBody.beta_version != vm.CurrentVersion)
            {
                bool result = false;
                DialogManager.CreateDialog()
.WithTitle(GetTranslation("Settings_NewVersionAvailable"))
.WithContent((String)JsonConvert.SerializeObject(convertedBody.beta_content))
.WithActionButton("Yes", _ => result = true, true)
.WithActionButton("No", _ => result = false, true)
.TryShow();
                if (result == true)
                {
                    UrlUtilities.OpenURL("https://toolbox.uotan.cn");
                }
            }
            else
            {

                _ = DialogManager.CreateDialog().WithTitle("Error").OfType(NotificationType.Error).WithContent(GetTranslation("Settings_UpToDate")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        catch (HttpRequestException e)
        {
            _ = DialogManager.CreateDialog().WithTitle("Error").WithActionButton("知道了", _ => { }, true).WithContent(e.Message).TryShow();
        }
    }
}