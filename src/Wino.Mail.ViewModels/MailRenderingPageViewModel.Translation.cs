using CommunityToolkit.Mvvm.Input;
using Wino.Core.Domain.Enums;

namespace Wino.Mail.ViewModels;

public partial class MailRenderingPageViewModel
{
    [RelayCommand]
    private void OpenTranslationSettings()
        => _navigationService.Navigate(WinoPage.SettingsPage, WinoPage.ReadComposePanePage, NavigationReferenceFrame.ShellFrame);
}
