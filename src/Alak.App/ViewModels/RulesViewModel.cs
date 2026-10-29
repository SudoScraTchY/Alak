using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using Alak.App.Design;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Alak.App.ViewModels;

public partial class RulesViewModel : ObservableObject
{
    private readonly IUserRulesRepository _rulesRepo;

    // Static helpers for picker ItemsSource
    public static IEnumerable<Category> CategoryValues => Enum.GetValues<Category>();
    public static IEnumerable<Category> AvailableCategories => Enum.GetValues<Category>();
    public static IEnumerable<UserRuleActionType> RuleActionValues => Enum.GetValues<UserRuleActionType>();
    public static IEnumerable<RuleMatchMode> RuleMatchModeValues => Enum.GetValues<RuleMatchMode>();
    public static IEnumerable<CaptureDepth> AvailableCaptureDepths => Enum.GetValues<CaptureDepth>();

    [ObservableProperty]
    private ObservableCollection<UserRuleViewModel> _rules = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _newRulePackage = "";

    partial void OnNewRulePackageChanged(string value)
    {
        if (AddRuleCommand is AsyncRelayCommand cmd)
        {
            cmd.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty]
    private string _newRulePattern = "";

    partial void OnNewRulePatternChanged(string value)
    {
        if (AddRuleCommand is AsyncRelayCommand cmd)
        {
            cmd.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty]
    private Category _newRuleCategory = Category.Offers;

    [ObservableProperty]
    private UserRuleActionType _newRuleAction = UserRuleActionType.Allow;

    [ObservableProperty]
    private RuleMatchMode _newRuleMatchMode = RuleMatchMode.Contains;

    [ObservableProperty]
    private int _newRulePriority = 0;

    [ObservableProperty]
    private bool _newRuleCaseSensitive = false;

    public ICommand AddRuleCommand { get; }
    public ICommand DeleteRuleCommand { get; }
    public ICommand ToggleRuleCommand { get; }
    public ICommand ReorderRulesCommand { get; }
    public ICommand LoadRulesCommand { get; }
    public ICommand BackCommand { get; }

    public RulesViewModel(IUserRulesRepository rulesRepo)
    {
        _rulesRepo = rulesRepo;

        AddRuleCommand = new AsyncRelayCommand(AddRuleAsync, CanAddRule);
        DeleteRuleCommand = new AsyncRelayCommand<UserRuleViewModel>(DeleteRuleAsync);
        ToggleRuleCommand = new AsyncRelayCommand<UserRuleViewModel>(ToggleRuleAsync);
        ReorderRulesCommand = new AsyncRelayCommand(ReorderRulesAsync);
        LoadRulesCommand = new AsyncRelayCommand(LoadRulesAsync);
        BackCommand = new AsyncRelayCommand(async () => await Shell.Current.GoToAsync(".."));

        _ = LoadRulesAsync();
    }

    private bool CanAddRule() => !string.IsNullOrWhiteSpace(NewRulePattern) || !string.IsNullOrWhiteSpace(NewRulePackage);

    private async Task LoadRulesAsync()
    {
        IsLoading = true;
        try
        {
            var rules = await _rulesRepo.GetAllRulesAsync();
            Rules.Clear();
            foreach (var rule in rules.OrderBy(r => r.Priority).ThenBy(r => r.Id))
            {
                Rules.Add(new UserRuleViewModel(rule));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddRuleAsync()
    {
        if (!CanAddRule()) return;

        var pkg = string.IsNullOrWhiteSpace(NewRulePackage) ? null : NewRulePackage.Trim();
        var pat = string.IsNullOrWhiteSpace(NewRulePattern) ? null : NewRulePattern.Trim();

        var match = new UserRuleMatch
        {
            Package = pkg,
            TextContains = pat
        };

        var action = new UserRuleAction
        {
            SetCategory = NewRuleCategory.ToKebabId(),
            Mute = NewRuleAction == UserRuleActionType.Mute,
            ArchiveImmediately = NewRuleAction == UserRuleActionType.Archive,
            AddTags = NewRuleAction == UserRuleActionType.Tag ? new[] { "user-tag" } : null
        };

        var rule = new UserRule(
            Id: 0,
            Priority: NewRulePriority,
            Match: match,
            Action: action,
            Enabled: true,
            CreatedAt: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            UpdatedAt: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            MatchMode = NewRuleMatchMode,
            CaseSensitive = NewRuleCaseSensitive
        };

        var id = await _rulesRepo.InsertRuleAsync(rule);
        rule = rule with { Id = id };
        Rules.Add(new UserRuleViewModel(rule));

        // Reset form
        NewRulePackage = "";
        NewRulePattern = "";
        NewRulePriority = 0;
    }

    private async Task DeleteRuleAsync(UserRuleViewModel? ruleVm)
    {
        if (ruleVm == null) return;
        await _rulesRepo.DeleteRuleAsync(ruleVm.Rule.Id);
        Rules.Remove(ruleVm);
    }

    private async Task ToggleRuleAsync(UserRuleViewModel? ruleVm)
    {
        if (ruleVm == null) return;
        var rule = ruleVm.Rule with { Enabled = !ruleVm.Rule.Enabled, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        await _rulesRepo.UpdateRuleAsync(rule);
        ruleVm.Rule = rule;
        ruleVm.Refresh();
    }

    private async Task ReorderRulesAsync()
    {
        var orderedIds = Rules.Select(r => r.Rule.Id).ToList();
        await _rulesRepo.ReorderRulesAsync(orderedIds);
    }
}

public partial class UserRuleViewModel : ObservableObject
{
    private UserRule _rule;

    public UserRuleViewModel(UserRule rule)
    {
        _rule = rule;
    }

    public UserRule Rule 
    { 
        get => _rule; 
        set
        {
            if (SetProperty(ref _rule, value))
            {
                OnPropertyChanged(nameof(Pattern));
                OnPropertyChanged(nameof(Package));
                OnPropertyChanged(nameof(HasPackage));
                OnPropertyChanged(nameof(HasPattern));
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(Category));
                OnPropertyChanged(nameof(ActionType));
                OnPropertyChanged(nameof(MatchMode));
                OnPropertyChanged(nameof(Priority));
                OnPropertyChanged(nameof(Enabled));
                OnPropertyChanged(nameof(EnabledText));
                OnPropertyChanged(nameof(CategoryColor));
            }
        }
    }

    public string Pattern => Rule.Match.TextContains ?? "";
    public string? Package => Rule.Match.Package;
    public bool HasPackage => !string.IsNullOrWhiteSpace(Package);
    public bool HasPattern => !string.IsNullOrWhiteSpace(Pattern);

    public string DisplayTitle
    {
        get
        {
            if (HasPackage && HasPattern) return $"{Package} · \"{Pattern}\"";
            if (HasPackage) return $"App: {Package}";
            if (HasPattern) return $"\"{Pattern}\"";
            return "(All notifications)";
        }
    }

    public string Category => Rule.Action.SetCategory ?? "other";
    public string ActionType => Rule.ActionType.ToString();
    public string MatchMode => Rule.MatchMode.ToString();
    public int Priority => Rule.Priority;
    public bool Enabled => Rule.Enabled;
    public string EnabledText => Rule.Enabled ? "Enabled" : "Disabled";

    public Color CategoryColor => Color.FromArgb(Category.ToLowerInvariant() switch
    {
        "promo" or "offers" => DesignTokens.CategoryMark.Promo,
        "work" => DesignTokens.CategoryMark.Work,
        "people" => DesignTokens.CategoryMark.People,
        "money" => DesignTokens.CategoryMark.Money,
        "orders" => DesignTokens.CategoryMark.Orders,
        "security" => DesignTokens.CategoryMark.Security,
        "social" => DesignTokens.CategoryMark.Social,
        "updates" => DesignTokens.CategoryMark.Updates,
        _ => DesignTokens.CategoryMark.Other
    });

    public void Refresh()
    {
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(EnabledText));
    }
}