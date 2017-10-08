﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using CharacterMap.Core;
using CharacterMap.Services;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;

namespace CharacterMap.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<Character> _chars;
        private string _fontIcon;
        private ObservableCollection<InstalledFont> _fontList;
        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        private bool _isLightThemeEnabled;
        private Character _selectedChar;
        private InstalledFont _selectedFont;
        private string _xamlCode;
        private string _symbolIcon;
        private bool _isBusy;

        public MainViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            RefreshFontList();
            IsLightThemeEnabled = ThemeSelectorService.IsLightThemeEnabled;
            CommandSavePng = new RelayCommand<bool>(async (b) => await SavePng(b));
            SwitchThemeCommand = new RelayCommand(async () => { await ThemeSelectorService.SwitchThemeAsync(); });
        }

        public ICommand SwitchThemeCommand { get; }

        public IDialogService DialogService { get; set; }

        public RelayCommand<bool> CommandSavePng { get; set; }

        public ObservableCollection<InstalledFont> FontList
        {
            get => _fontList;
            set
            {
                _fontList = value;
                CreateFontListGroup();
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get => _groupedFontList;
            set
            {
                _groupedFontList = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<Character> Chars
        {
            get => _chars;
            set
            {
                _chars = value;
                RaisePropertyChanged();
            }
        }

        public Character SelectedChar
        {
            get => _selectedChar;
            set
            {
                _selectedChar = value;
                if (null != value)
                {
                    XamlCode = $"&#x{value.UnicodeIndex.ToString("x").ToUpper()};";
                    FontIcon = $@"<FontIcon FontFamily=""{SelectedFont.Name}"" Glyph=""&#x{
                            value.UnicodeIndex.ToString("x").ToUpper()
                        };"" />";
                    SymbolIcon = $"(Symbol)0x{value.UnicodeIndex.ToString("x").ToUpper()}";
                }
                RaisePropertyChanged();
            }
        }

        public string XamlCode
        {
            get => _xamlCode;
            set
            {
                _xamlCode = value;
                RaisePropertyChanged();
            }
        }

        public string SymbolIcon
        {
            get => _symbolIcon;
            set { _symbolIcon = value; RaisePropertyChanged(); }
        }

        public string FontIcon
        {
            get => _fontIcon;
            set
            {
                _fontIcon = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowSymbolFontsOnly
        {
            get => App.AppSettings.ShowSymbolFontsOnly;
            set
            {
                App.AppSettings.ShowSymbolFontsOnly = value;
                RefreshFontList();
                RaisePropertyChanged();
            }
        }

        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                _selectedFont = value;
                if (null != _selectedFont)
                {
                    App.AppSettings.DefaultSelectedFontName = value.Name;
                    LoadCharsAsync(_selectedFont);
                }

                RaisePropertyChanged();
            }
        }

        public bool IsLightThemeEnabled
        {
            get => _isLightThemeEnabled;
            set => Set(ref _isLightThemeEnabled, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; RaisePropertyChanged(); }
        }

        private void LoadCharsAsync(InstalledFont font)
        {
            var chars = font.GetCharacters();
            Chars = chars.ToObservableCollection();
        }

        private void RefreshFontList()
        {
            try
            {
                var fontList = InstalledFont.GetFonts();
                FontList = fontList.Where(f => f.IsSymbolFont || !ShowSymbolFontsOnly)
                    .OrderBy(f => f.Name)
                    .ToObservableCollection();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font List");
            }
        }

        private void CreateFontListGroup()
        {
            try
            {
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1), true);
                GroupedFontList = list.ToObservableCollection();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font Group");
            }
        }

        private async Task SavePng(bool isBlackText)
        {
            try
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                savePicker.FileTypeChoices.Add("Png Image", new[] { ".png" });
                savePicker.SuggestedFileName = $"CharacterMap_{DateTime.Now:yyyyMMddHHmmss}.png";
                StorageFile file = await savePicker.PickSaveFileAsync();

                if (null != file)
                {
                    CachedFileManager.DeferUpdates(file);
                    CanvasDevice device = CanvasDevice.GetSharedDevice();
                    var localDpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                    var canvasH = (float) App.AppSettings.PngSize;
                    var canvasW = (float) App.AppSettings.PngSize;

                    CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, canvasW, canvasH, localDpi);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);
                        var d = App.AppSettings.PngSize;
                        var r = App.AppSettings.PngSize / 2;

                        var textColor = isBlackText ? Colors.Black : Colors.White;

                        var fontSize = (float) d;
                        if (SelectedFont.Name == "Segoe UI Emoji")
                        {
                            fontSize *= 0.75f;
                        }

                        ds.DrawText(SelectedChar.Char, (float)r, 0, textColor, new CanvasTextFormat
                        {
                            FontFamily = SelectedFont.Name,
                            FontSize = fontSize,
                            HorizontalAlignment = CanvasHorizontalAlignment.Center
                        });
                    }

                    using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
                    }

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowMessageBox(ex.Message, "Error Saving Image");
            }
        }
    }
}