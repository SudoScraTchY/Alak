using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Alak.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace Alak.App.ViewModels;

public sealed class CuratedModelItem
{
    public string Title { get; }
    public string Description { get; }
    public string FormattedSize { get; }
    public string Quantization { get; }
    public string Url { get; }
    public ICommand OpenDownloadUrlCommand { get; }

    public CuratedModelItem(string title, string description, string formattedSize, string quantization, string url)
    {
        Title = title;
        Description = description;
        FormattedSize = formattedSize;
        Quantization = quantization;
        Url = url;

        OpenDownloadUrlCommand = new AsyncRelayCommand(async () =>
        {
            if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            {
                await Launcher.Default.OpenAsync(uri);
            }
        });
    }
}

public partial class ModelsViewModel : ObservableObject
{
    private readonly IModelService _modelService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private double _importProgress;

    [ObservableProperty]
    private string _importStatusText = string.Empty;

    [ObservableProperty]
    private string _benchmarkResult = string.Empty;

    [ObservableProperty]
    private bool _isRunningBenchmark;

    public ObservableCollection<InstalledModelInfo> InstalledModels { get; } = new();
    public ObservableCollection<CuratedModelItem> CuratedModels { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ImportModelCommand { get; }
    public ICommand SetActiveModelCommand { get; }
    public ICommand DeleteModelCommand { get; }
    public ICommand RunBenchmarkCommand { get; }
    public ICommand BackCommand { get; }

    public ModelsViewModel(IModelService modelService)
    {
        _modelService = modelService;

        RefreshCommand = new RelayCommand(LoadInstalledModels);
        ImportModelCommand = new AsyncRelayCommand(ImportModelAsync);
        SetActiveModelCommand = new AsyncRelayCommand<InstalledModelInfo>(SetActiveModelAsync);
        DeleteModelCommand = new AsyncRelayCommand<InstalledModelInfo>(DeleteModelAsync);
        RunBenchmarkCommand = new AsyncRelayCommand(RunBenchmarkAsync);
        BackCommand = new AsyncRelayCommand(async () =>
        {
            await Shell.Current.GoToAsync("..");
        });

        InitializeCuratedCatalog();
        LoadInstalledModels();

        LocalizationManager.Instance.PropertyChanged += (_, _) =>
        {
            InitializeCuratedCatalog();
            LoadInstalledModels();
        };
    }

    private void InitializeCuratedCatalog()
    {
        var loc = LocalizationManager.Instance;
        CuratedModels.Clear();

        CuratedModels.Add(new CuratedModelItem(
            title: loc["Models_Smol135Title"],
            description: loc["Models_Smol135Desc"],
            formattedSize: "~98 MB",
            quantization: "Q4_K_M",
            url: "https://huggingface.co/HuggingFaceTB/SmolLM2-135M-Instruct-GGUF/resolve/main/smollm2-135m-instruct-q4_k_m.gguf"));

        CuratedModels.Add(new CuratedModelItem(
            title: loc["Models_Smol360Title"],
            description: loc["Models_Smol360Desc"],
            formattedSize: "~230 MB",
            quantization: "Q4_K_M",
            url: "https://huggingface.co/HuggingFaceTB/SmolLM2-360M-Instruct-GGUF/resolve/main/smollm2-360m-instruct-q4_k_m.gguf"));

        CuratedModels.Add(new CuratedModelItem(
            title: loc["Models_Qwen05Title"],
            description: loc["Models_Qwen05Desc"],
            formattedSize: "~390 MB",
            quantization: "Q4_K_M",
            url: "https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/main/qwen2.5-0.5b-instruct-q4_k_m.gguf"));
    }

    public void LoadInstalledModels()
    {
        InstalledModels.Clear();
        var models = _modelService.GetInstalledModels();
        foreach (var m in models)
        {
            InstalledModels.Add(m);
        }
    }

    private async Task ImportModelAsync()
    {
        if (IsImporting) return;

        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select GGUF model file (.gguf)"
            };

            var result = await FilePicker.Default.PickAsync(options);
            if (result == null) return;

            IsImporting = true;
            ImportProgress = 0.0;
            ImportStatusText = "Verifying and copying model...";

            var progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ImportProgress = p;
                });
            });

            bool success = await _modelService.ImportModelAsync(result, progress, CancellationToken.None);

            var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
            if (success)
            {
                LoadInstalledModels();
                if (page != null)
                {
                    await page.DisplayAlertAsync("Alak", LocalizationManager.Instance["Model_ImportSuccess"], LocalizationManager.Instance["Common_OK"]);
                }
            }
            else
            {
                if (page != null)
                {
                    await page.DisplayAlertAsync("Alak", LocalizationManager.Instance["Model_InvalidHeader"], LocalizationManager.Instance["Common_OK"]);
                }
            }
        }
        catch (Exception ex)
        {
            var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Alak", ex.Message, LocalizationManager.Instance["Common_OK"]);
            }
        }
        finally
        {
            IsImporting = false;
            ImportProgress = 0.0;
            ImportStatusText = string.Empty;
        }
    }

    private async Task SetActiveModelAsync(InstalledModelInfo? model)
    {
        if (model == null) return;
        await _modelService.SetActiveModelAsync(model.Path);
        LoadInstalledModels();
    }

    private async Task DeleteModelAsync(InstalledModelInfo? model)
    {
        if (model == null) return;

        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            bool confirm = await page.DisplayAlertAsync("Delete Model", $"Remove '{model.Name}' from storage?", "Delete", "Cancel");
            if (!confirm) return;
        }

        await _modelService.DeleteModelAsync(model.Path);
        LoadInstalledModels();
    }

    private async Task RunBenchmarkAsync()
    {
        if (IsRunningBenchmark) return;

        try
        {
            IsRunningBenchmark = true;
            BenchmarkResult = "Benchmarking decode speed...";
            BenchmarkResult = await _modelService.BenchmarkAsync();
        }
        catch (Exception ex)
        {
            BenchmarkResult = $"Benchmark error: {ex.Message}";
        }
        finally
        {
            IsRunningBenchmark = false;
        }
    }
}
