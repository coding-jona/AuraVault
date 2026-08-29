using AuraVault.Core.Generator;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

public partial class GeneratorViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _passphraseMode;

    [ObservableProperty]
    private int _length = 20;

    [ObservableProperty]
    private int _words = 5;

    [ObservableProperty]
    private bool _symbols = true;

    [ObservableProperty]
    private bool _digits = true;

    [ObservableProperty]
    private bool _excludeLookAlike = true;

    [ObservableProperty]
    private string _result = "";

    [ObservableProperty]
    private string _strength = "";

    public GeneratorViewModel() => Regenerate();

    partial void OnPassphraseModeChanged(bool value) => Regenerate();

    partial void OnLengthChanged(int value) => Regenerate();

    partial void OnWordsChanged(int value) => Regenerate();

    partial void OnSymbolsChanged(bool value) => Regenerate();

    partial void OnDigitsChanged(bool value) => Regenerate();

    partial void OnExcludeLookAlikeChanged(bool value) => Regenerate();

    [RelayCommand]
    private void Regenerate()
    {
        double bits;
        if (PassphraseMode)
        {
            var profile = new PassphraseProfile { WordCount = System.Math.Max(3, Words) };
            Result = PasswordGenerator.GeneratePassphrase(profile);
            bits = EntropyEstimator.PassphraseBits(profile.WordCount, EffLargeWordList.Instance.Count);
        }
        else
        {
            var profile = new CharacterProfile
            {
                Length = System.Math.Clamp(Length, 6, 128),
                Symbols = Symbols,
                Digits = Digits,
                ExcludeLookAlike = ExcludeLookAlike,
            };
            Result = PasswordGenerator.Generate(profile);
            bits = EntropyEstimator.PoolBits(Result);
        }

        Strength = $"~{bits:F0} bits · {EntropyEstimator.Classify(bits)}";
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyAsync() =>
        await AuraVault.App.Services.ClipboardHelper.SetTextAsync(Result);
}
